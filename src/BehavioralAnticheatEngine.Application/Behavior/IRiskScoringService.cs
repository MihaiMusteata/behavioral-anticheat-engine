using BehavioralAnticheatEngine.Domain.Behavior;

namespace BehavioralAnticheatEngine.Application.Behavior;

public interface IRiskScoringService
{
    Task<RiskScoringResult> ScoreAsync(SessionFeatureAggregate aggregate, CancellationToken cancellationToken = default);

    Task<RiskScoringResult> ScoreEventsAsync(IEnumerable<string> eventTypes, CancellationToken cancellationToken = default);
}

public sealed record RiskScoringResult(double Score, string Reason);
