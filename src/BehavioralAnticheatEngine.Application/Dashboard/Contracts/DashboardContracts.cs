using BehavioralAnticheatEngine.Application.Detection;

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
    SessionDetectionSummaryDto Detection,
    int TotalEvents,
    string LiveStatus);

// Low / Medium / High come from the AI ensemble level of completed
// detections; Processing = submitted and not scored yet (or retrying);
// Waiting = not submitted yet.
public sealed record RiskDistributionDto(int Low, int Medium, int High, int Processing, int Waiting);

public sealed record EventFrequencyDto(string EventType, int Count);

public sealed record SessionTimelineDto(
    Guid SessionId,
    Guid StudentId,
    string StudentEmail,
    string Status,
    SessionDetectionSummaryDto Detection,
    bool? SelfReportedCheated,
    Guid? SelectedQuestionId,
    IReadOnlyCollection<TimelineQuestionDto> Questions,
    IReadOnlyCollection<AnswerReviewDto> Answers,
    IReadOnlyCollection<TimelineEventDto> Events);

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

public sealed record TimelineQuestionDto(
    Guid QuestionId,
    int OrderIndex,
    string Label,
    string Prompt,
    QuestionDetectionDto? Detection,
    IReadOnlyCollection<TimelineQuestionOptionDto> Options);

public sealed record TimelineQuestionOptionDto(Guid OptionId, string Label);

public sealed record LiveSessionState(
    Guid ExamScheduleId,
    Guid SessionId,
    Guid StudentId,
    string StudentEmail,
    string Status,
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
