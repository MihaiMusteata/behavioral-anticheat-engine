namespace BehavioralAnticheatEngine.Domain.Exams;

public sealed class Question
{
    private readonly List<QuestionOption> _options = [];

    private Question()
    {
    }

    private Question(
        Guid assessmentId,
        string type,
        string prompt,
        decimal points,
        int orderIndex)
    {
        Id = Guid.NewGuid();
        AssessmentId = assessmentId;
        Type = type;
        Prompt = prompt;
        Points = points;
        OrderIndex = orderIndex;
    }

    public Guid Id { get; private set; }

    public Guid AssessmentId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Prompt { get; private set; } = string.Empty;

    public decimal Points { get; private set; }

    public int OrderIndex { get; private set; }

    public Assessment? Assessment { get; private set; }

    public IReadOnlyCollection<QuestionOption> Options => _options;

    public static Question Create(
        Guid assessmentId,
        string type,
        string prompt,
        decimal points,
        int orderIndex,
        IEnumerable<QuestionOptionInput> options)
    {
        Validate(type, prompt, points);

        var question = new Question(assessmentId, type, prompt.Trim(), points, orderIndex);
        question.ReplaceOptions(options);

        return question;
    }

    public void Update(
        string type,
        string prompt,
        decimal points,
        int orderIndex,
        IEnumerable<QuestionOptionInput> options)
    {
        Validate(type, prompt, points);

        Type = type;
        Prompt = prompt.Trim();
        Points = points;
        OrderIndex = orderIndex;
        ReplaceOptions(options);
    }

    public bool RequiresManualScoring()
    {
        return Type == QuestionTypes.FreeText;
    }

    private void ReplaceOptions(IEnumerable<QuestionOptionInput> options)
    {
        _options.Clear();

        if (Type == QuestionTypes.FreeText)
        {
            return;
        }

        var materialized = options.ToArray();
        if (materialized.Length < 2)
        {
            throw new ArgumentException("Choice questions require at least two options.", nameof(options));
        }

        if (!materialized.Any(option => option.IsCorrect))
        {
            throw new ArgumentException("Choice questions require at least one correct option.", nameof(options));
        }

        foreach (var option in materialized)
        {
            if (string.IsNullOrWhiteSpace(option.Label))
            {
                throw new ArgumentException("Option label is required.", nameof(options));
            }

            _options.Add(QuestionOption.Create(Id, option.Label.Trim(), option.IsCorrect, option.OrderIndex));
        }
    }

    private static void Validate(string type, string prompt, decimal points)
    {
        if (!QuestionTypes.All.Contains(type, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Unsupported question type.");
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Question prompt is required.", nameof(prompt));
        }

        if (points <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Question points must be positive.");
        }
    }
}
