namespace BehavioralAnticheatEngine.Domain.Behavior;

public sealed class ScoringRule
{
    private ScoringRule()
    {
    }

    private ScoringRule(string featureKey, double weight, bool isEnabled)
    {
        Id = Guid.NewGuid();
        FeatureKey = featureKey;
        Weight = weight;
        IsEnabled = isEnabled;
    }

    public Guid Id { get; private set; }

    public string FeatureKey { get; private set; } = string.Empty;

    public double Weight { get; private set; }

    public bool IsEnabled { get; private set; }

    public static ScoringRule Create(string featureKey, double weight, bool isEnabled = true)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            throw new ArgumentException("Feature key is required.", nameof(featureKey));
        }

        return new ScoringRule(featureKey.Trim(), weight, isEnabled);
    }
}
