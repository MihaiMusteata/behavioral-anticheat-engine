using System.Text.Json;
using BehavioralAnticheatEngine.Application.Detection;
using BehavioralAnticheatEngine.Infrastructure.Messaging;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BehavioralAnticheatEngine.Infrastructure.Workers;

public sealed record DetectionOptions
{
    public const string SectionName = "Detection";

    // Wait this long after submit before scoring: the client flushes its last
    // batch of behavioral events at submit time and they reach the database
    // through Kafka + this Worker a few seconds later.
    public int SettleSeconds { get; init; } = 15;

    // Sessions scored per ml-service call (one call amortises TabPFN's cost).
    public int MaxBatchSize { get; init; } = 25;

    // After the first message, keep collecting this long to fill a batch.
    public int BatchWindowMilliseconds { get; init; } = 3000;

    public int MaxAttempts { get; init; } = 3;

    public int SweepIntervalSeconds { get; init; } = 60;

    // Must exceed the worst-case queue wait (a full exam submitted at once
    // takes a while to drain on CPU), otherwise healthy queued rows get
    // re-queued as duplicates (harmless - skipped at processing - but noisy).
    public int StaleProcessingMinutes { get; init; } = 30;

    public int RetryFailedAfterMinutes { get; init; } = 2;

    // Queue submitted sessions that predate the AI scoring (no detection row).
    public bool BackfillSubmittedSessions { get; init; } = true;

    // Backfill at most this many per sweep, so old sessions trickle in at
    // roughly the processing rate instead of flooding the queue.
    public int BackfillBatchSize { get; init; } = 50;
}

/// <summary>
/// Consumes the detection queue in batches and hands them to
/// IDetectionProcessingService (build payloads -> ml-service -> store ->
/// notify dashboard). Offsets are committed after a batch is processed; a
/// crash before that re-delivers the batch, and already-completed sessions
/// are skipped, so processing is at-least-once without duplicate results.
/// </summary>
public sealed class DetectionConsumerHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaOptions _kafka;
    private readonly DetectionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DetectionConsumerHostedService> _logger;

    public DetectionConsumerHostedService(
        IServiceProvider serviceProvider,
        IOptions<KafkaOptions> kafka,
        IOptions<DetectionOptions> options,
        TimeProvider timeProvider,
        ILogger<DetectionConsumerHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _kafka = kafka.Value;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await EnsureTopicExistsAsync(stoppingToken);

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            GroupId = _kafka.DetectionConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true,
            // A batch can take minutes on CPU; stay in the group meanwhile.
            MaxPollIntervalMs = 15 * 60 * 1000
        }).Build();
        consumer.Subscribe(_kafka.DetectionRequestsTopic);
        _logger.LogInformation("Detection worker subscribed to topic {Topic}.", _kafka.DetectionRequestsTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = CollectBatch(consumer, stoppingToken);
                if (batch.Count == 0)
                {
                    continue;
                }

                var requests = batch
                    .Select(consumed => TryParse(consumed.Message.Value))
                    .OfType<DetectionRequest>()
                    .ToArray();

                await WaitForSettleAsync(requests, stoppingToken);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var processing = scope.ServiceProvider.GetRequiredService<IDetectionProcessingService>();
                    await processing.ProcessBatchAsync(requests, stoppingToken);
                }

                // Commit the next offset of every partition seen in this batch.
                consumer.Commit(batch
                    .GroupBy(consumed => consumed.TopicPartition)
                    .Select(group => new TopicPartitionOffset(group.Key, new Offset(group.Max(consumed => consumed.Offset.Value) + 1)))
                    .ToArray());
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Not committed: the batch is re-delivered after a rebalance or
                // restart, and the recovery sweep re-queues stale rows anyway.
                _logger.LogError(exception, "Detection batch failed; retrying shortly.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
    }

    private List<ConsumeResult<string, string>> CollectBatch(IConsumer<string, string> consumer, CancellationToken stoppingToken)
    {
        var batch = new List<ConsumeResult<string, string>>();
        var first = consumer.Consume(stoppingToken);
        if (first?.Message is null)
        {
            return batch;
        }

        batch.Add(first);
        var deadline = DateTime.UtcNow.AddMilliseconds(_options.BatchWindowMilliseconds);
        while (batch.Count < _options.MaxBatchSize)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var next = consumer.Consume(remaining);
            if (next?.Message is null)
            {
                break;
            }

            batch.Add(next);
        }

        return batch;
    }

    private async Task WaitForSettleAsync(IReadOnlyCollection<DetectionRequest> requests, CancellationToken stoppingToken)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var readyAt = requests.Max(request => request.SubmittedAtUtc).AddSeconds(_options.SettleSeconds);
        var wait = readyAt - _timeProvider.GetUtcNow();
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait > TimeSpan.FromSeconds(_options.SettleSeconds) ? TimeSpan.FromSeconds(_options.SettleSeconds) : wait, stoppingToken);
        }
    }

    private DetectionRequest? TryParse(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<DetectionRequest>(value);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Skipping malformed detection request.");
            return null;
        }
    }

    private async Task EnsureTopicExistsAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 10 && !cancellationToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = _kafka.BootstrapServers }).Build();
                await adminClient.CreateTopicsAsync(
                [
                    new TopicSpecification { Name = _kafka.DetectionRequestsTopic, NumPartitions = 3, ReplicationFactor = 1 }
                ]);
                return;
            }
            catch (CreateTopicsException exception)
                when (exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                return;
            }
            catch (Exception exception) when (attempt < 10)
            {
                _logger.LogWarning(exception, "Detection topic initialization failed (attempt {Attempt}); retrying.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt, 5)), cancellationToken);
            }
        }
    }
}
