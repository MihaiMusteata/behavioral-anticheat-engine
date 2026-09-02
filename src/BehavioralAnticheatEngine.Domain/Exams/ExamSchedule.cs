using System.Security.Cryptography;

namespace BehavioralAnticheatEngine.Domain.Exams;

public sealed class ExamSchedule
{
    private ExamSchedule()
    {
    }

    private ExamSchedule(
        Guid assessmentId,
        string name,
        int durationMinutes,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        AssessmentId = assessmentId;
        Name = name;
        DurationMinutes = durationMinutes;
        StartsAtUtc = null;
        EndsAtUtc = null;
        AccessPinCode = null;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        Status = ExamStatuses.Scheduled;
    }

    public Guid Id { get; private set; }

    public Guid AssessmentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int DurationMinutes { get; private set; }

    public DateTimeOffset? StartsAtUtc { get; private set; }

    public DateTimeOffset? EndsAtUtc { get; private set; }

    public string? AccessPinCode { get; private set; }

    public string Status { get; private set; } = ExamStatuses.Scheduled;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ManuallyStartedAtUtc { get; private set; }

    public DateTimeOffset? ManuallyStoppedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public Assessment? Assessment { get; private set; }

    public static ExamSchedule Create(
        Guid assessmentId,
        string name,
        int durationMinutes,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        Validate(name, durationMinutes);

        return new ExamSchedule(assessmentId, name.Trim(), durationMinutes, createdByUserId, createdAtUtc);
    }

    public static string GeneratePinCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    public void AssignAccessPinCode(string pinCode)
    {
        if (string.IsNullOrWhiteSpace(pinCode))
        {
            throw new ArgumentException("Access PIN code is required.", nameof(pinCode));
        }

        AccessPinCode = pinCode;
    }

    public void Update(string name, int durationMinutes)
    {
        Validate(name, durationMinutes);

        Name = name.Trim();
        DurationMinutes = durationMinutes;
    }

    public void Start(DateTimeOffset startedAtUtc)
    {
        Status = ExamStatuses.Running;

        if (ManuallyStartedAtUtc is null)
        {
            StartsAtUtc = startedAtUtc;
            EndsAtUtc = startedAtUtc.AddMinutes(DurationMinutes);
        }

        ManuallyStartedAtUtc ??= startedAtUtc;
    }

    public void Stop(DateTimeOffset stoppedAtUtc)
    {
        Status = ExamStatuses.Stopped;
        ManuallyStoppedAtUtc ??= stoppedAtUtc;
    }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
    }

    public bool CanJoin(DateTimeOffset nowUtc)
    {
        return Status == ExamStatuses.Running;
    }

    private static void Validate(string name, int durationMinutes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Exam name is required.", nameof(name));
        }

        if (durationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Duration must be positive.");
        }
    }
}
