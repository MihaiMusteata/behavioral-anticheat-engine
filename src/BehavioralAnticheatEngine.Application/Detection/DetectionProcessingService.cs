using System.Text.Json;
using System.Text.Json.Nodes;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Dashboard.Contracts;
using BehavioralAnticheatEngine.Domain.Behavior;
using Microsoft.Extensions.Logging;

namespace BehavioralAnticheatEngine.Application.Detection;

/// <summary>
/// Scores a batch of submitted sessions: builds their payloads from the
/// database, calls ml-service once for the whole batch, stores every result
/// and notifies the live dashboard. A batch amortises the per-call cost of
/// the voters (TabPFN re-reads its training context on every call).
/// </summary>
public sealed class DetectionProcessingService : IDetectionProcessingService
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDetectionRepository _detections;
    private readonly DetectionPayloadBuilder _payloads;
    private readonly IMlDetectionClient _ml;
    private readonly IDashboardEventBus _dashboardEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DetectionProcessingService> _logger;

    public DetectionProcessingService(
        IDetectionRepository detections,
        DetectionPayloadBuilder payloads,
        IMlDetectionClient ml,
        IDashboardEventBus dashboardEvents,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<DetectionProcessingService> logger)
    {
        _detections = detections;
        _payloads = payloads;
        _ml = ml;
        _dashboardEvents = dashboardEvents;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ProcessBatchAsync(IReadOnlyList<DetectionRequest> requests, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var rows = (await _detections.ListForSessionsAsync(requests.Select(request => request.SessionId).Distinct().ToArray(), cancellationToken))
            .ToDictionary(row => row.SessionId);

        // Skip duplicates (a re-delivered message for a session already scored
        // after its latest request) and requests whose row disappeared.
        var work = new List<(SessionDetection Row, JsonObject Payload, Guid StudentId)>();
        foreach (var request in requests.GroupBy(request => request.SessionId).Select(group => group.First()))
        {
            if (!rows.TryGetValue(request.SessionId, out var row))
            {
                continue;
            }

            if (row.Status == SessionDetectionStatuses.Completed && row.CompletedAtUtc >= row.RequestedAtUtc)
            {
                continue;
            }

            var built = await _payloads.BuildAsync(request.SessionId, cancellationToken);
            if (built is null)
            {
                row.Fail("Exam session was not found.", now);
                continue;
            }

            row.MarkStarted(now);
            work.Add((row, built.Value.Payload, built.Value.Session.StudentId));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (work.Count == 0)
        {
            return;
        }

        IReadOnlyList<JsonObject> results;
        try
        {
            results = await _ml.DetectAsync(work.Select(item => item.Payload).ToArray(), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "ml-service failed for a batch of {Count} sessions.", work.Count);
            var failedAt = _timeProvider.GetUtcNow();
            foreach (var item in work)
            {
                item.Row.Fail($"ml-service error: {exception.Message}", failedAt);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await PublishAsync(work.Select(item => (item.Row, item.StudentId)), cancellationToken);
            return;
        }

        var bySession = results
            .Where(result => result["sessionId"] is not null)
            .ToDictionary(result => result["sessionId"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
        var completedAt = _timeProvider.GetUtcNow();
        foreach (var item in work)
        {
            if (!bySession.TryGetValue(item.Row.SessionId.ToString(), out var result))
            {
                item.Row.Fail("ml-service returned no result for this session.", completedAt);
                continue;
            }

            var session = result["session"] as JsonObject;
            var ensemble = session?["ensemble"] as JsonObject;
            var score = ensemble?["score"] is JsonValue scoreValue && scoreValue.GetValueKind() == JsonValueKind.Number
                ? scoreValue.GetValue<double>()
                : (double?)null;

            item.Row.Complete(
                score,
                ensemble?["decision"]?.GetValue<string>() ?? "insufficient_data",
                ensemble?["level"]?.GetValue<string>() ?? "unknown",
                ensemble?["method"]?.GetValue<string>() ?? string.Empty,
                ensemble?["votersUsed"]?.GetValue<int>() ?? 0,
                session?["models"]?.ToJsonString() ?? "{}",
                result["questions"]?.ToJsonString() ?? "[]",
                result["questionSummary"]?.ToJsonString() ?? "{}",
                completedAt);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Detection completed for {Count} sessions.", work.Count);
        await PublishAsync(work.Select(item => (item.Row, item.StudentId)), cancellationToken);
    }

    private async Task PublishAsync(IEnumerable<(SessionDetection Row, Guid StudentId)> items, CancellationToken cancellationToken)
    {
        foreach (var (row, studentId) in items)
        {
            try
            {
                // No liveState in the payload on purpose: the dashboard treats
                // it as "something changed" and refetches the exam snapshot.
                await _dashboardEvents.PublishAsync(
                    new DashboardLiveEvent(
                        row.ExamScheduleId,
                        row.SessionId,
                        studentId,
                        "detection.updated",
                        JsonSerializer.Serialize(new { sessionId = row.SessionId, detectionStatus = row.Status, score = row.Score }, WebJsonOptions),
                        _timeProvider.GetUtcNow()),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not publish detection update for session {SessionId}.", row.SessionId);
            }
        }
    }
}
