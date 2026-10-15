namespace BehavioralAnticheatEngine.Application.Exams.Contracts;

public sealed record ExamScheduleDto(
    Guid Id,
    Guid AssessmentId,
    string AssessmentTitle,
    string Name,
    int DurationMinutes,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string Status,
    string AccessPinCode,
    string AntiCaptureMode);

public sealed record CreateExamScheduleRequest(
    Guid AssessmentId,
    string Name,
    int DurationMinutes,
    string? AntiCaptureMode = null);

public sealed record UpdateExamScheduleRequest(
    string Name,
    int DurationMinutes,
    string? AntiCaptureMode = null);
