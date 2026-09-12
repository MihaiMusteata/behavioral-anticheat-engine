using System.Text.Json;

namespace BehavioralAnticheatEngine.Application.Behavior.Contracts;

public sealed record BehavioralEventBatchRequest(IReadOnlyCollection<BehavioralEventEnvelope> Events);

public sealed record BehavioralEventEnvelope(
    Guid SessionId,
    long Seq,
    DateTimeOffset Timestamp,
    string Nonce,
    string Data,
    string Signature);

public sealed record BehavioralEventBatchResult(
    int AcceptedCount,
    int RejectedCount,
    IReadOnlyCollection<BehavioralEventResult> Results);

public sealed record BehavioralEventResult(long Seq, bool Accepted, string? Reason, string? Flags);

public sealed record ValidatedBehavioralEvent(
    Guid SessionId,
    Guid ExamScheduleId,
    Guid StudentId,
    long Seq,
    DateTimeOffset TimestampUtc,
    string Nonce,
    Guid? QuestionId,
    string EventType,
    string PayloadJson,
    string Signature,
    string ValidationFlags,
    DateTimeOffset ReceivedAtUtc);

public sealed record BehavioralIngestionOptions
{
    public const string SectionName = "BehavioralIngestion";

    public int AcceptedClockSkewSeconds { get; init; } = 120;

    public int NonceTtlSeconds { get; init; } = 600;

    public long LargeSequenceGapThreshold { get; init; } = 20;

    public int MaxEventsPerMinute { get; init; } = 240;

    public int EventSilenceSeconds { get; init; } = 60;

    public int MaxBatchSize { get; init; } = 100;

    public int MaxEncryptedDataLength { get; init; } = 90_000;
}
