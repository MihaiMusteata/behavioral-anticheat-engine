namespace BehavioralAnticheatEngine.Domain.Exams;

public sealed class ExamSession
{
    private ExamSession()
    {
    }

    private ExamSession(
        Guid examScheduleId,
        Guid assessmentId,
        Guid studentId,
        string participantName,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endsAtUtc,
        DateTimeOffset consentAcceptedAtUtc)
    {
        Id = Guid.NewGuid();
        ExamScheduleId = examScheduleId;
        AssessmentId = assessmentId;
        StudentId = studentId;
        ParticipantName = participantName;
        StartedAtUtc = startedAtUtc;
        EndsAtUtc = endsAtUtc;
        ConsentAcceptedAtUtc = consentAcceptedAtUtc;
        LastActivityAtUtc = startedAtUtc;
        Status = ExamSessionStatuses.Active;
    }

    public Guid Id { get; private set; }

    public Guid ExamScheduleId { get; private set; }

    public Guid AssessmentId { get; private set; }

    public Guid StudentId { get; private set; }

    public string ParticipantName { get; private set; } = string.Empty;

    public string Status { get; private set; } = ExamSessionStatuses.Active;

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public DateTimeOffset LastActivityAtUtc { get; private set; }

    public DateTimeOffset ConsentAcceptedAtUtc { get; private set; }

    public double CurrentRiskScore { get; private set; }

    public int SequenceGapWarnings { get; private set; }

    public bool? SelfReportedCheated { get; private set; }

    public DateTimeOffset? SelfReportedAtUtc { get; private set; }

    public ExamSchedule? ExamSchedule { get; private set; }

    public Assessment? Assessment { get; private set; }

    public static ExamSession Start(
        Guid examScheduleId,
        Guid assessmentId,
        Guid studentId,
        string participantName,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endsAtUtc,
        DateTimeOffset consentAcceptedAtUtc)
    {
        return new ExamSession(examScheduleId, assessmentId, studentId, participantName, startedAtUtc, endsAtUtc, consentAcceptedAtUtc);
    }

    public void MarkActivity(DateTimeOffset activityAtUtc)
    {
        if (activityAtUtc > LastActivityAtUtc)
        {
            LastActivityAtUtc = activityAtUtc;
        }
    }

    public void Submit(DateTimeOffset submittedAtUtc)
    {
        if (Status == ExamSessionStatuses.Submitted)
        {
            return;
        }

        Status = ExamSessionStatuses.Submitted;
        SubmittedAtUtc = submittedAtUtc;
        LastActivityAtUtc = submittedAtUtc;
    }

    public void UpdateRisk(double riskScore)
    {
        CurrentRiskScore = Math.Clamp(riskScore, 0, 100);
    }

    public void RecordSequenceGap()
    {
        SequenceGapWarnings++;
    }

    public void RecordSelfReport(bool cheated, DateTimeOffset reportedAtUtc)
    {
        SelfReportedCheated = cheated;
        SelfReportedAtUtc = reportedAtUtc;
    }
}
