namespace BehavioralAnticheatEngine.Infrastructure.Messaging;

public sealed record KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = "localhost:19092";

    public string BehavioralEventsTopic { get; init; } = "behavioral-events";

    public string ConsumerGroupId { get; init; } = "behavioral-anticheat-worker";
}
