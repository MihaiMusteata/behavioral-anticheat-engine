namespace BehavioralAnticheatEngine.Domain.Exams;

// Deliberately NOT part of the behavioral_events audit stream (no Kafka
// publish, no worker, no risk-score recalculation) - this is a one-off
// baseline reading captured on the join screen, not a live anti-cheat
// signal, and keeping it in its own table/pipeline means a bug here can
// never affect the exam-taking critical path.
public sealed class CalibrationSample
{
    private CalibrationSample()
    {
    }

    private CalibrationSample(
        Guid sessionId,
        string fieldName,
        int characterCount,
        int totalDurationMs,
        double averageCharsPerSecond,
        int correctionCount,
        string keystrokeTimingsJson,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        FieldName = fieldName;
        CharacterCount = characterCount;
        TotalDurationMs = totalDurationMs;
        AverageCharsPerSecond = averageCharsPerSecond;
        CorrectionCount = correctionCount;
        KeystrokeTimingsJson = keystrokeTimingsJson;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public string FieldName { get; private set; } = string.Empty;

    public int CharacterCount { get; private set; }

    public int TotalDurationMs { get; private set; }

    public double AverageCharsPerSecond { get; private set; }

    public int CorrectionCount { get; private set; }

    // Array of {index, dwellMs, flightMs} per character - the actual key
    // pressed is never recorded, only timing, so this can't be used to
    // reconstruct what was typed.
    public string KeystrokeTimingsJson { get; private set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static CalibrationSample Create(
        Guid sessionId,
        string fieldName,
        int characterCount,
        int totalDurationMs,
        double averageCharsPerSecond,
        int correctionCount,
        string keystrokeTimingsJson,
        DateTimeOffset createdAtUtc)
    {
        return new CalibrationSample(
            sessionId,
            fieldName,
            characterCount,
            totalDurationMs,
            averageCharsPerSecond,
            correctionCount,
            keystrokeTimingsJson,
            createdAtUtc);
    }
}
