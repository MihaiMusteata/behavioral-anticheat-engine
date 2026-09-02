namespace BehavioralAnticheatEngine.Domain.Exams;

public static class QuestionTypes
{
    public const string SingleChoice = "single_choice";
    public const string MultipleChoice = "multiple_choice";
    public const string FreeText = "free_text";

    public static readonly string[] All = [SingleChoice, MultipleChoice, FreeText];
}
