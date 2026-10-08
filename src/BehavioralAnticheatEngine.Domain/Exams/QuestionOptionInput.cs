namespace BehavioralAnticheatEngine.Domain.Exams;

public sealed record QuestionOptionInput(string Label, bool IsCorrect, int OrderIndex);
