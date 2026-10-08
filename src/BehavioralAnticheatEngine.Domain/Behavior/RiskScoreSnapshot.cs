namespace BehavioralAnticheatEngine.Domain.Behavior;

public sealed class RiskScoreSnapshot
{
    private RiskScoreSnapshot()
    {
    }

    private RiskScoreSnapshot(Guid sessionId, Guid examScheduleId, Guid studentId, double score, string reason, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        ExamScheduleId = examScheduleId;
        StudentId = studentId;
        Score = Math.Clamp(score, 0, 100);
        Reason = reason;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid ExamScheduleId { get; private set; }

    public Guid StudentId { get; private set; }

    public double Score { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static RiskScoreSnapshot Create(Guid sessionId, Guid examScheduleId, Guid studentId, double score, string reason, DateTimeOffset createdAtUtc)
    {
        return new RiskScoreSnapshot(sessionId, examScheduleId, studentId, score, reason, createdAtUtc);
    }
}
