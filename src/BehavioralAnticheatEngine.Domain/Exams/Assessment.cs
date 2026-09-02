namespace BehavioralAnticheatEngine.Domain.Exams;

public sealed class Assessment
{
    private readonly List<Question> _questions = [];

    private Assessment()
    {
    }

    private Assessment(
        string title,
        string? description,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        Title = title;
        Description = description;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public bool IsPublished { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public IReadOnlyCollection<Question> Questions => _questions;

    public static Assessment Create(
        string title,
        string? description,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        Validate(title);

        return new Assessment(title.Trim(), NormalizeOptional(description), createdByUserId, createdAtUtc);
    }

    public void Update(string title, string? description, DateTimeOffset updatedAtUtc)
    {
        Validate(title);

        Title = title.Trim();
        Description = NormalizeOptional(description);
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Publish()
    {
        IsPublished = true;
    }

    public void Unpublish()
    {
        IsPublished = false;
    }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        IsPublished = false;
        DeletedAtUtc = deletedAtUtc;
    }

    private static void Validate(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Assessment title is required.", nameof(title));
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
