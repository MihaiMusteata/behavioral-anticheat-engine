using System.Text.Json;

namespace BehavioralAnticheatEngine.Domain.Behavior;

public sealed class SessionFeatureAggregate
{
    private SessionFeatureAggregate()
    {
    }

    private SessionFeatureAggregate(Guid sessionId, Guid examScheduleId, Guid studentId, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        ExamScheduleId = examScheduleId;
        StudentId = studentId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid ExamScheduleId { get; private set; }

    public Guid StudentId { get; private set; }

    public int TotalEvents { get; private set; }

    public int FocusLostCount { get; private set; }

    public int VisibilityHiddenCount { get; private set; }

    public int CopyCount { get; private set; }

    public int PasteCount { get; private set; }

    public int FullscreenExitCount { get; private set; }

    public int DevtoolsSignals { get; private set; }

    public int InactivitySignals { get; private set; }

    public int SequenceGapWarnings { get; private set; }

    public string FeatureCountsJson { get; private set; } = "{}";

    public double RiskScore { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SessionFeatureAggregate Create(Guid sessionId, Guid examScheduleId, Guid studentId, DateTimeOffset createdAtUtc)
    {
        return new SessionFeatureAggregate(sessionId, examScheduleId, studentId, createdAtUtc);
    }

    public void RegisterEvent(string eventType, DateTimeOffset updatedAtUtc)
    {
        TotalEvents++;
        UpdatedAtUtc = updatedAtUtc;
        IncrementFeature(ToFeatureKey(eventType));
    }

    public void RegisterFocusLost()
    {
        FocusLostCount++;
    }

    public void RegisterVisibilityHidden()
    {
        VisibilityHiddenCount++;
    }

    public void RegisterCopy()
    {
        CopyCount++;
    }

    public void RegisterPaste()
    {
        PasteCount++;
    }

    public void RegisterFullscreenExit()
    {
        FullscreenExitCount++;
    }

    public void RegisterDevtoolsSignal()
    {
        DevtoolsSignals++;
    }

    public void RegisterInactivitySignal()
    {
        InactivitySignals++;
    }

    public void RegisterSequenceGapWarning()
    {
        SequenceGapWarnings++;
        IncrementFeature("sequence_gap_warnings");
    }

    public void SetRiskScore(double riskScore, DateTimeOffset updatedAtUtc)
    {
        RiskScore = Math.Clamp(riskScore, 0, 100);
        UpdatedAtUtc = updatedAtUtc;
    }

    public IReadOnlyDictionary<string, int> GetFeatureCounts()
    {
        if (string.IsNullOrWhiteSpace(FeatureCountsJson))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(FeatureCountsJson)
                ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void IncrementFeature(string featureKey)
    {
        var counts = new Dictionary<string, int>(GetFeatureCounts(), StringComparer.OrdinalIgnoreCase);
        counts[featureKey] = counts.TryGetValue(featureKey, out var current) ? current + 1 : 1;
        FeatureCountsJson = JsonSerializer.Serialize(counts.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    private static string ToFeatureKey(string eventType)
    {
        return eventType
            .Trim()
            .Replace(".", "_", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal)
            .ToLowerInvariant() + "_count";
    }
}
