using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Domain.Behavior;
using Microsoft.EntityFrameworkCore;

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Repositories;

public sealed class BehavioralEventRepository : IBehavioralEventRepository
{
    private readonly AppDbContext _dbContext;

    public BehavioralEventRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddEventAsync(BehavioralEventRecord eventRecord, CancellationToken cancellationToken = default)
    {
        await _dbContext.BehavioralEvents.AddAsync(eventRecord, cancellationToken);
    }

    public Task<bool> EventExistsAsync(Guid sessionId, long seq, CancellationToken cancellationToken = default)
    {
        return _dbContext.BehavioralEvents
            .AsNoTracking()
            .AnyAsync(behavioralEvent => behavioralEvent.SessionId == sessionId && behavioralEvent.Seq == seq, cancellationToken);
    }

    public Task<List<BehavioralEventRecord>> ListEventsForSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.BehavioralEvents
            .AsNoTracking()
            .Where(behavioralEvent => behavioralEvent.SessionId == sessionId)
            .OrderBy(behavioralEvent => behavioralEvent.ReceivedAtUtc)
            .ThenBy(behavioralEvent => behavioralEvent.TimestampUtc)
            .ThenBy(behavioralEvent => behavioralEvent.Seq)
            .ToListAsync(cancellationToken);
    }

    public Task<SessionFeatureAggregate?> GetFeatureAggregateAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SessionFeatureAggregates
            .FirstOrDefaultAsync(aggregate => aggregate.SessionId == sessionId, cancellationToken);
    }

    public async Task AddFeatureAggregateAsync(SessionFeatureAggregate aggregate, CancellationToken cancellationToken = default)
    {
        await _dbContext.SessionFeatureAggregates.AddAsync(aggregate, cancellationToken);
    }

    public async Task AddRiskSnapshotAsync(RiskScoreSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _dbContext.RiskScoreSnapshots.AddAsync(snapshot, cancellationToken);
    }

    public Task<List<RiskScoreSnapshot>> ListRiskSnapshotsForSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.RiskScoreSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.SessionId == sessionId)
            .OrderBy(snapshot => snapshot.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ScoringRule>> ListScoringRulesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.ScoringRules
            .AsNoTracking()
            .Where(rule => rule.IsEnabled)
            .OrderBy(rule => rule.FeatureKey)
            .ToListAsync(cancellationToken);
    }
}
