namespace BehavioralAnticheatEngine.Application.Exams.Contracts;

public sealed record AssessmentDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsPublished,
    IReadOnlyCollection<QuestionDto> Questions);

public sealed record AssessmentSummaryDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsPublished,
    int QuestionCount);

public sealed record QuestionDto(
    Guid Id,
    string Type,
    string Prompt,
    decimal Points,
    int OrderIndex,
    IReadOnlyCollection<QuestionOptionDto> Options);

public sealed record QuestionOptionDto(Guid Id, string Label, bool IsCorrect, int OrderIndex);

public sealed record QuestionOptionRequest(string Label, bool IsCorrect, int OrderIndex);

public sealed record CreateAssessmentRequest(string Title, string? Description, bool Publish);

public sealed record UpdateAssessmentRequest(string Title, string? Description, bool Publish);

public sealed record UpsertQuestionRequest(
    string Type,
    string Prompt,
    decimal Points,
    int OrderIndex,
    IReadOnlyCollection<QuestionOptionRequest> Options);
