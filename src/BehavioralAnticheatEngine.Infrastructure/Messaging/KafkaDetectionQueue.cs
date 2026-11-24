using System.Text.Json;
using BehavioralAnticheatEngine.Application.Detection;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BehavioralAnticheatEngine.Infrastructure.Messaging;

/// <summary>
/// Detection queue on Kafka (Redpanda): the API produces one message per
/// submitted session, the Worker's DetectionConsumerHostedService consumes
/// them in batches. Keyed by session id, so retries of one session stay in
/// order on the same partition.
/// </summary>
public sealed class KafkaDetectionQueue : IDetectionQueue, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaDetectionQueue> _logger;

    public KafkaDetectionQueue(IOptions<KafkaOptions> options, ILogger<KafkaDetectionQueue> logger)
    {
        _options = options.Value;
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 10_000
        }).Build();
    }

    public async Task EnqueueAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _producer.ProduceAsync(
            _options.DetectionRequestsTopic,
            new Message<string, string>
            {
                Key = request.SessionId.ToString("N"),
                Value = JsonSerializer.Serialize(request)
            },
            cancellationToken);
        _logger.LogInformation("Queued detection for session {SessionId} at {TopicPartitionOffset}.", request.SessionId, result.TopicPartitionOffset);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(2));
        _producer.Dispose();
    }
}
