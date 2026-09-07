using System.Diagnostics;
using System.Text.Json;
using BehavioralAnticheatEngine.Application.Behavior.Contracts;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Observability;
using BehavioralAnticheatEngine.Infrastructure.Observability;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BehavioralAnticheatEngine.Infrastructure.Messaging;

public sealed class KafkaBehavioralEventPublisher : IBehavioralEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaBehavioralEventPublisher> _logger;

    public KafkaBehavioralEventPublisher(
        IOptions<KafkaOptions> options,
        ILogger<KafkaBehavioralEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
    }

    public async Task PublishAsync(ValidatedBehavioralEvent behavioralEvent, CancellationToken cancellationToken = default)
    {
        using var activity = KafkaActivitySource.Source.StartActivity(
            $"{_options.BehavioralEventsTopic} publish",
            ActivityKind.Producer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", _options.BehavioralEventsTopic);
        activity?.SetTag("behavioral.session_id", behavioralEvent.SessionId);
        activity?.SetTag("behavioral.seq", behavioralEvent.Seq);

        var message = new Message<string, string>
        {
            Key = behavioralEvent.SessionId.ToString("N"),
            Value = JsonSerializer.Serialize(behavioralEvent),
            Headers = new Headers()
        };
        KafkaTraceContextPropagator.Inject(activity, message.Headers);

        try
        {
            var result = await _producer.ProduceAsync(_options.BehavioralEventsTopic, message, cancellationToken);
            activity?.SetTag("messaging.kafka.partition", result.Partition.Value);
            activity?.SetTag("messaging.kafka.offset", result.Offset.Value);
            AppMetrics.KafkaEventsPublished.Add(1, new KeyValuePair<string, object?>("outcome", "success"));
            _logger.LogDebug(
                "Published behavioral event {SessionId}/{Seq} to {TopicPartitionOffset}.",
                behavioralEvent.SessionId,
                behavioralEvent.Seq,
                result.TopicPartitionOffset);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            AppMetrics.KafkaEventsPublished.Add(1, new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(2));
        _producer.Dispose();
    }
}
