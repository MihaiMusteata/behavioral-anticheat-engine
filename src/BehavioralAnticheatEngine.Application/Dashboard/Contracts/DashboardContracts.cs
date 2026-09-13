namespace BehavioralAnticheatEngine.Application.Dashboard.Contracts;

public sealed record ExamDashboardDto(
    Guid ExamScheduleId,
    string Name,
    IReadOnlyCollection<DashboardSessionRowDto> Sessions,
    RiskDistributionDto RiskDistribution,
    IReadOnlyCollection<EventFrequencyDto> EventFrequencies);

public sealed record DashboardSessionRowDto(
    Guid SessionId,
    Guid StudentId,
    string StudentEmail,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    double RiskScore,
    int TotalEvents,
    string LiveStatus);

public sealed record RiskDistributionDto(int Low, int Medium, int High);

public sealed record EventFrequencyDto(string EventType, int Count);

public sealed record SessionTimelineDto(
    Guid SessionId,
    Guid StudentId,
    string StudentEmail,
    string Status,
    double RiskScore,
    bool? SelfReportedCheated,
    Guid? SelectedQuestionId,
    double? SelectedQuestionRiskScore,
    IReadOnlyCollection<TimelineQuestionDto> Questions,
    IReadOnlyCollection<AnswerReviewDto> Answers,
    IReadOnlyCollection<TimelineEventDto> Events,
    IReadOnlyCollection<RiskSnapshotDto> RiskSnapshots);

public sealed record SessionTimelineExportDto(
    string FileName,
    string ContentType,
    string Content);

public sealed record AnswerReviewDto(
    Guid QuestionId,
    string Prompt,
    string AnswerJson,
    bool RequiresManualScoring,
    decimal? AutoScore,
    decimal? ManualScore,
    string? ManualFeedback);

public sealed record TimelineEventDto(
    Guid Id,
    long Seq,
    DateTimeOffset TimestampUtc,
    DateTimeOffset ReceivedAtUtc,
    Guid? QuestionId,
    string EventType,
    string PayloadJson,
    string ValidationFlags);

public sealed record RiskSnapshotDto(double Score, string Reason, DateTimeOffset CreatedAtUtc);

public sealed record TimelineQuestionDto(
    Guid QuestionId,
    int OrderIndex,
    string Label,
    string Prompt,
    double RiskScore);

public sealed record LiveSessionState(
    Guid ExamScheduleId,
    Guid SessionId,
    Guid StudentId,
    string StudentEmail,
    string Status,
    double RiskScore,
    int TotalEvents,
    DateTimeOffset LastActivityAtUtc,
    string LastEventType);

public sealed record DashboardLiveEvent(
    Guid ExamScheduleId,
    Guid SessionId,
    Guid StudentId,
    string Type,
    string PayloadJson,
    DateTimeOffset OccurredAtUtc);
