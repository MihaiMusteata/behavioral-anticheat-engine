using System.Diagnostics;
using System.Text.Json;
using BehavioralAnticheatEngine.Application.Behavior;
using BehavioralAnticheatEngine.Application.Behavior.Contracts;
using BehavioralAnticheatEngine.Application.Observability;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using BehavioralAnticheatEngine.Infrastructure.Messaging;
using BehavioralAnticheatEngine.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace BehavioralAnticheatEngine.Infrastructure.Workers;

public sealed class BehavioralEventConsumerHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaOptions _options;
    private readonly ILogger<BehavioralEventConsumerHostedService> _logger;
    private readonly ResiliencePipeline _processingPipeline;

    public BehavioralEventConsumerHostedService(
        IServiceProvider serviceProvider,
        IOptions<KafkaOptions> options,
        ILogger<BehavioralEventConsumerHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
        _processingPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(250),
                BackoffType = DelayBackoffType.Exponential
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 8,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(10)
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AllowAutoCreateTopics = true
        };

        await EnsureTopicExistsAsync(stoppingToken);

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_options.BehavioralEventsTopic);

        _logger.LogInformation("Behavioral event worker subscribed to topic {Topic}.", _options.BehavioralEventsTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumed = null;
            Activity? activity = null;

            try
            {
                consumed = consumer.Consume(stoppingToken);
                if (consumed?.Message?.Value is null)
                {
                    if (consumed is not null)
                    {
                        consumer.Commit(consumed);
                    }

                    continue;
                }

                var behavioralEvent = JsonSerializer.Deserialize<ValidatedBehavioralEvent>(consumed.Message.Value);
                if (behavioralEvent is null)
                {
                    throw new JsonException("Kafka behavioral event payload deserialized to null.");
                }

                var parentContext = KafkaTraceContextPropagator.Extract(consumed.Message.Headers);
                activity = KafkaActivitySource.Source.StartActivity(
                    $"{_options.BehavioralEventsTopic} process",
                    ActivityKind.Consumer,
                    parentContext);
                activity?.SetTag("messaging.system", "kafka");
                activity?.SetTag("messaging.kafka.partition", consumed.Partition.Value);
                activity?.SetTag("messaging.kafka.offset", consumed.Offset.Value);
                activity?.SetTag("behavioral.session_id", behavioralEvent.SessionId);
                activity?.SetTag("behavioral.seq", behavioralEvent.Seq);

                var stopwatch = Stopwatch.StartNew();

                await _processingPipeline.ExecuteAsync(
                    async token =>
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var processor = scope.ServiceProvider.GetRequiredService<IBehavioralEventProcessingService>();
                        await processor.ProcessAsync(behavioralEvent, token);
                    },
                    stoppingToken);

                consumer.Commit(consumed);
                AppMetrics.KafkaProcessingDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
                AppMetrics.KafkaEventsConsumed.Add(1, new KeyValuePair<string, object?>("outcome", "processed"));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (BrokenCircuitException exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "circuit_open");
                AppMetrics.KafkaEventsConsumed.Add(1, new KeyValuePair<string, object?>("outcome", "circuit_open"));
                RewindFailedMessage(consumer, consumed);
                _logger.LogWarning(exception, "Behavioral processing circuit is open; delaying consumption.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (ConsumeException exception)
            {
                AppMetrics.KafkaEventsConsumed.Add(1, new KeyValuePair<string, object?>("outcome", "consume_error"));
                _logger.LogError(exception, "Kafka consume error.");
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                AppMetrics.KafkaEventsConsumed.Add(1, new KeyValuePair<string, object?>("outcome", "error"));
                RewindFailedMessage(consumer, consumed);
                _logger.LogError(
                    exception,
                    "Failed to process behavioral event at {TopicPartitionOffset}; the partition will retry from that offset.",
                    consumed?.TopicPartitionOffset);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            finally
            {
                activity?.Dispose();
            }
        }
    }

    private void RewindFailedMessage(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string>? consumed)
    {
        if (consumed is null)
        {
            return;
        }

        try
        {
            consumer.Seek(consumed.TopicPartitionOffset);
        }
        catch (KafkaException exception)
        {
            _logger.LogError(
                exception,
                "Could not rewind Kafka consumer to failed offset {TopicPartitionOffset}.",
                consumed.TopicPartitionOffset);
            throw;
        }
    }

    private async Task EnsureTopicExistsAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 12;

        for (var attempt = 1; ; attempt++)
        {
            using var adminClient = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = _options.BootstrapServers
            }).Build();

            try
            {
                await adminClient.CreateTopicsAsync(
                [
                    new TopicSpecification
                    {
                        Name = _options.BehavioralEventsTopic,
                        NumPartitions = 12,
                        ReplicationFactor = 1
                    }
                ]);

                return;
            }
            catch (CreateTopicsException exception)
                when (exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                _logger.LogDebug("Kafka topic {Topic} already exists.", _options.BehavioralEventsTopic);
                return;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(attempt, 5));
                _logger.LogWarning(
                    exception,
                    "Kafka topic initialization failed on attempt {Attempt}/{MaxAttempts}; retrying in {DelaySeconds}s.",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(
                    exception,
                    "Kafka topic initialization failed after {MaxAttempts} attempts.",
                    maxAttempts);
                throw;
            }
        }
    }
}
