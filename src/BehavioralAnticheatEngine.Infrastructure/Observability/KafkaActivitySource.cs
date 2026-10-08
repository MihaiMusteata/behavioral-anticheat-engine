using System.Diagnostics;

namespace BehavioralAnticheatEngine.Infrastructure.Observability;

public static class KafkaActivitySource
{
    public const string Name = "BehavioralAnticheatEngine.Kafka";

    public static readonly ActivitySource Source = new(Name);
}
