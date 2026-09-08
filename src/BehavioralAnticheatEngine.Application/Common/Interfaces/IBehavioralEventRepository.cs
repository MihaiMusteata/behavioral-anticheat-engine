using BehavioralAnticheatEngine.Domain.Behavior;

namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface IBehavioralEventRepository
{
    Task AddEventAsync(BehavioralEventRecord eventRecord, CancellationToken cancellationToken = default);

    Task<bool> EventExistsAsync(Guid sessionId, long seq, CancellationToken cancellationToken = default);

    Task<List<BehavioralEventRecord>> ListEventsForSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<SessionFeatureAggregate?> GetFeatureAggregateAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task AddFeatureAggregateAsync(SessionFeatureAggregate aggregate, CancellationToken cancellationToken = default);

    Task AddRiskSnapshotAsync(RiskScoreSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<List<RiskScoreSnapshot>> ListRiskSnapshotsForSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<List<ScoringRule>> ListScoringRulesAsync(CancellationToken cancellationToken = default);
}
