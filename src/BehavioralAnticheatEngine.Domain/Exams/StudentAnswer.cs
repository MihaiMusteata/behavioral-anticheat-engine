namespace BehavioralAnticheatEngine.Domain.Exams;

public sealed class StudentAnswer
{
    private StudentAnswer()
    {
    }

    private StudentAnswer(
        Guid sessionId,
        Guid questionId,
        string answerJson,
        bool requiresManualScoring,
        DateTimeOffset answeredAtUtc)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        QuestionId = questionId;
        AnswerJson = answerJson;
        RequiresManualScoring = requiresManualScoring;
        AnsweredAtUtc = answeredAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid QuestionId { get; private set; }

    public string AnswerJson { get; private set; } = "{}";

    public DateTimeOffset AnsweredAtUtc { get; private set; }

    public bool RequiresManualScoring { get; private set; }

    public decimal? AutoScore { get; private set; }

    public decimal? ManualScore { get; private set; }

    public string? ManualFeedback { get; private set; }

    public static StudentAnswer Create(
        Guid sessionId,
        Guid questionId,
        string answerJson,
        bool requiresManualScoring,
        DateTimeOffset answeredAtUtc)
    {
        return new StudentAnswer(sessionId, questionId, answerJson, requiresManualScoring, answeredAtUtc);
    }

    public void Update(string answerJson, bool requiresManualScoring, DateTimeOffset answeredAtUtc)
    {
        AnswerJson = answerJson;
        RequiresManualScoring = requiresManualScoring;
        AnsweredAtUtc = answeredAtUtc;
        AutoScore = null;
    }

    public void SetAutoScore(decimal score)
    {
        AutoScore = score;
    }

    public void SetManualScore(decimal score, string? feedback)
    {
        ManualScore = score;
        ManualFeedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
    }
}
