using System.Text.Json.Nodes;

namespace BehavioralAnticheatEngine.Application.Detection;

/// <summary>Queue message: score this submitted session.</summary>
public sealed record DetectionRequest(Guid SessionId, Guid ExamScheduleId, DateTimeOffset SubmittedAtUtc);

/// <summary>One voter's opinion, as shown in the dashboard tooltip.</summary>
public sealed record DetectionModelVoteDto(string Model, double? Probability, string Vote);

/// <summary>
/// Suspicion shown in the dashboard. Status: "waiting" (not submitted yet),
/// "processing" (queued / being scored), "completed", "failed".
/// </summary>
public sealed record SessionDetectionSummaryDto(
    string Status,
    double? Score,
    string? Decision,
    string? Level,
    string? Method,
    int? VotersUsed,
    IReadOnlyCollection<DetectionModelVoteDto> Models,
    DateTimeOffset? RequestedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? Error);

// Question level: majority vote of 3 voters decides (SuspectVotes of
// VotersUsed), Score is the mean of their probabilities.
public sealed record QuestionDetectionDto(
    Guid QuestionId,
    int QuestionIndex,
    double? Score,
    string? Decision,
    string? Level,
    int? SuspectVotes,
    int? VotersUsed,
    IReadOnlyCollection<DetectionModelVoteDto> Models,
    IReadOnlyDictionary<string, double?> Evidence);

public static class DetectionDisplayStatuses
{
    public const string Waiting = "waiting";
}

public interface IDetectionQueue
{
    Task EnqueueAsync(DetectionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>HTTP client of ml-service (POST /v1/detect).</summary>
public interface IMlDetectionClient
{
    /// <summary>Returns one result object per payload, keyed by "sessionId".</summary>
    Task<IReadOnlyList<JsonObject>> DetectAsync(IReadOnlyList<JsonObject> payloads, CancellationToken cancellationToken = default);
}

public interface IDetectionRequestService
{
    /// <summary>Mark the (just submitted) session as processing and put it on the queue.</summary>
    Task RequestAsync(Guid sessionId, Guid examScheduleId, DateTimeOffset submittedAtUtc, CancellationToken cancellationToken = default);
}

public interface IDetectionProcessingService
{
    Task ProcessBatchAsync(IReadOnlyList<DetectionRequest> requests, CancellationToken cancellationToken = default);
}
