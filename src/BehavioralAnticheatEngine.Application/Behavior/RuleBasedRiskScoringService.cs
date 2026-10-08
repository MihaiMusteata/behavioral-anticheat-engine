using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Domain.Behavior;

namespace BehavioralAnticheatEngine.Application.Behavior;

public sealed class RuleBasedRiskScoringService : IRiskScoringService
{
    private readonly IBehavioralEventRepository _events;

    public RuleBasedRiskScoringService(IBehavioralEventRepository events)
    {
        _events = events;
    }

    public async Task<RiskScoringResult> ScoreAsync(SessionFeatureAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var rules = await _events.ListScoringRulesAsync(cancellationToken);
        var enabledRules = rules.Where(rule => rule.IsEnabled).ToDictionary(rule => rule.FeatureKey, rule => rule.Weight, StringComparer.OrdinalIgnoreCase);
        var featureCounts = new Dictionary<string, int>(aggregate.GetFeatureCounts(), StringComparer.OrdinalIgnoreCase)
        {
            ["focus_lost_count"] = Math.Max(GetCount(aggregate.GetFeatureCounts(), "focus_lost_count"), aggregate.FocusLostCount),
            ["visibility_hidden_count"] = Math.Max(GetCount(aggregate.GetFeatureCounts(), "visibility_hidden_count"), aggregate.VisibilityHiddenCount),
            ["copy_count"] = Math.Max(GetCount(aggregate.GetFeatureCounts(), "copy_count"), aggregate.CopyCount),
            ["paste_count"] = Math.Max(GetCount(aggregate.GetFeatureCounts(), "paste_count"), aggregate.PasteCount),
            ["fullscreen_exit_count"] = Math.Max(GetCount(aggregate.GetFeatureCounts(), "fullscreen_exit_count"), aggregate.FullscreenExitCount),
            ["devtools_heuristic_triggered_count"] = Math.Max(GetCount(aggregate.GetFeatureCounts(), "devtools_heuristic_triggered_count"), aggregate.DevtoolsSignals),
            ["idle_detected_count"] = Math.Max(GetCount(aggregate.GetFeatureCounts(), "idle_detected_count"), aggregate.InactivitySignals),
            ["sequence_gap_warnings"] = Math.Max(GetCount(aggregate.GetFeatureCounts(), "sequence_gap_warnings"), aggregate.SequenceGapWarnings)
        };

        var score = enabledRules.Sum(rule => rule.Value * GetCount(featureCounts, rule.Key));

        var normalized = Math.Clamp(score, 0, 100);
        var reason = normalized switch
        {
            >= 70 => "high behavioral risk based on configured mock scoring rules",
            >= 35 => "medium behavioral risk based on configured mock scoring rules",
            _ => "low behavioral risk based on configured mock scoring rules"
        };

        return new RiskScoringResult(normalized, reason);
    }

    public async Task<RiskScoringResult> ScoreEventsAsync(IEnumerable<string> eventTypes, CancellationToken cancellationToken = default)
    {
        var rules = await _events.ListScoringRulesAsync(cancellationToken);
        var enabledRules = rules.Where(rule => rule.IsEnabled).ToDictionary(rule => rule.FeatureKey, rule => rule.Weight, StringComparer.OrdinalIgnoreCase);
        var counts = eventTypes
            .Select(ToFeatureKey)
            .GroupBy(featureKey => featureKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var score = enabledRules.Sum(rule => rule.Value * GetCount(counts, rule.Key));
        var normalized = Math.Clamp(score, 0, 100);

        return new RiskScoringResult(normalized, "mock score scoped to selected audit events");
    }

    private static int GetCount(IReadOnlyDictionary<string, int> counts, string featureKey)
    {
        return counts.TryGetValue(featureKey, out var count) ? count : 0;
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
