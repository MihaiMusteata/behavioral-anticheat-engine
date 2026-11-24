namespace BehavioralAnticheatEngine.Domain.Behavior;

public static class SessionDetectionStatuses
{
    // Submitted and queued, or being scored by the ML service right now.
    public const string Processing = "processing";

    public const string Completed = "completed";

    // Scoring failed; the recovery sweep re-queues it until MaxAttempts.
    public const string Failed = "failed";
}

/// <summary>
/// AI suspicion result of one exam session, produced asynchronously after
/// submit by the voting ensemble (ml-service). Replaces the old rule-based
/// risk score. A session without a row has not been submitted yet ("waiting").
/// </summary>
public sealed class SessionDetection
{
    private SessionDetection()
    {
    }

    private SessionDetection(Guid sessionId, Guid examScheduleId, DateTimeOffset requestedAtUtc)
    {
        SessionId = sessionId;
        ExamScheduleId = examScheduleId;
        RequestedAtUtc = requestedAtUtc;
        Status = SessionDetectionStatuses.Processing;
    }

    public Guid SessionId { get; private set; }

    public Guid ExamScheduleId { get; private set; }

    public string Status { get; private set; } = SessionDetectionStatuses.Processing;

    public int Attempts { get; private set; }

    public DateTimeOffset RequestedAtUtc { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    // Ensemble score in [0, 1]; null until completed.
    public double? Score { get; private set; }

    public string? Decision { get; private set; }

    public string? Level { get; private set; }

    public string? Method { get; private set; }

    public int? VotersUsed { get; private set; }

    // Per-voter probabilities/votes (session level), as returned by ml-service.
    public string ModelsJson { get; private set; } = "{}";

    // Per-question results (models, ensemble, evidence) - for the audit view.
    public string QuestionsJson { get; private set; } = "[]";

    public string QuestionSummaryJson { get; private set; } = "{}";

    public string? Error { get; private set; }

    public static SessionDetection Request(Guid sessionId, Guid examScheduleId, DateTimeOffset requestedAtUtc)
    {
        return new SessionDetection(sessionId, examScheduleId, requestedAtUtc);
    }

    /// <summary>Re-queue (new submit of the same session, or retry after a failure).</summary>
    public void Requeue(DateTimeOffset requestedAtUtc)
    {
        Status = SessionDetectionStatuses.Processing;
        RequestedAtUtc = requestedAtUtc;
        Error = null;
    }

    public void MarkStarted(DateTimeOffset startedAtUtc)
    {
        Attempts++;
        StartedAtUtc = startedAtUtc;
    }

    public void Complete(
        double? score,
        string decision,
        string level,
        string method,
        int votersUsed,
        string modelsJson,
        string questionsJson,
        string questionSummaryJson,
        DateTimeOffset completedAtUtc)
    {
        Status = SessionDetectionStatuses.Completed;
        Score = score is null ? null : Math.Clamp(score.Value, 0, 1);
        Decision = decision;
        Level = level;
        Method = method;
        VotersUsed = votersUsed;
        ModelsJson = modelsJson;
        QuestionsJson = questionsJson;
        QuestionSummaryJson = questionSummaryJson;
        CompletedAtUtc = completedAtUtc;
        Error = null;
    }

    public void Fail(string error, DateTimeOffset failedAtUtc)
    {
        Status = SessionDetectionStatuses.Failed;
        Error = error.Length > 1000 ? error[..1000] : error;
        CompletedAtUtc = failedAtUtc;
    }
}
