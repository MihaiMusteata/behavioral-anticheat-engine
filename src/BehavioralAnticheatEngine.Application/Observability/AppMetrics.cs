using System.Diagnostics.Metrics;

namespace BehavioralAnticheatEngine.Application.Observability;

public static class AppMetrics
{
    public const string MeterName = "BehavioralAnticheatEngine";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> BehavioralEventsIngested = Meter.CreateCounter<long>(
        "behavioral_events_ingested_total",
        description: "Behavioral events processed by the ingestion pipeline, tagged by outcome and rejection reason.");

    public static readonly Counter<long> RateLimitRejections = Meter.CreateCounter<long>(
        "behavioral_rate_limit_rejections_total",
        description: "Behavioral ingestion batches rejected by the per-student rate limiter.");

    public static readonly Counter<long> AuthAttempts = Meter.CreateCounter<long>(
        "auth_login_attempts_total",
        description: "Login attempts, tagged by outcome.");

    public static readonly Counter<long> KafkaEventsPublished = Meter.CreateCounter<long>(
        "kafka_events_published_total",
        description: "Behavioral events published to Kafka, tagged by outcome.");

    public static readonly Counter<long> KafkaEventsConsumed = Meter.CreateCounter<long>(
        "kafka_events_consumed_total",
        description: "Behavioral events consumed from Kafka, tagged by outcome.");

    public static readonly Histogram<double> KafkaProcessingDuration = Meter.CreateHistogram<double>(
        "kafka_processing_duration_ms",
        unit: "ms",
        description: "Time spent processing a behavioral event consumed from Kafka, including downstream persistence.");
}
