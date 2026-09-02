namespace BehavioralAnticheatEngine.Domain.Exams;

public sealed class QuestionOption
{
    private QuestionOption()
    {
    }

    private QuestionOption(Guid questionId, string label, bool isCorrect, int orderIndex)
    {
        Id = Guid.NewGuid();
        QuestionId = questionId;
        Label = label;
        IsCorrect = isCorrect;
        OrderIndex = orderIndex;
    }

    public Guid Id { get; private set; }

    public Guid QuestionId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public bool IsCorrect { get; private set; }

    public int OrderIndex { get; private set; }

    public Question? Question { get; private set; }

    public static QuestionOption Create(Guid questionId, string label, bool isCorrect, int orderIndex)
    {
        return new QuestionOption(questionId, label, isCorrect, orderIndex);
    }
}
