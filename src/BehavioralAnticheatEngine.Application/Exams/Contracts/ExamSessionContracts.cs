using System.Text.Json;

namespace BehavioralAnticheatEngine.Application.Exams.Contracts;

public sealed record StartExamSessionResponse(
    Guid SessionId,
    Guid ExamScheduleId,
    Guid AssessmentId,
    string ParticipantName,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset ServerTimeUtc,
    string BehaviorSigningSecret,
    StudentAssessmentDto Assessment,
    IReadOnlyCollection<ExistingAnswerDto> ExistingAnswers);

public sealed record ExistingAnswerDto(Guid QuestionId, string AnswerJson);

public sealed record JoinExamGuestRequest(string PinCode, string ParticipantName, bool ConsentAccepted);

public sealed record GuestJoinResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    StartExamSessionResponse Session);

public sealed record StudentAssessmentDto(
    Guid Id,
    string Title,
    IReadOnlyCollection<StudentQuestionDto> Questions);

public sealed record StudentQuestionDto(
    Guid Id,
    string Type,
    string Prompt,
    decimal Points,
    int OrderIndex,
    IReadOnlyCollection<StudentQuestionOptionDto> Options);

public sealed record StudentQuestionOptionDto(Guid Id, string Label, int OrderIndex);

public sealed record SaveAnswerRequest(Guid QuestionId, JsonElement Answer);

public sealed record SaveAnswerResponse(Guid QuestionId, decimal? AutoScore, bool RequiresManualScoring);

public sealed record SubmitExamResponse(Guid SessionId, string Status, DateTimeOffset SubmittedAtUtc, decimal? Score);

public sealed record ManualScoreRequest(decimal Score, string? Feedback);

public sealed record SelfReportCheatingRequest(bool Cheated);
