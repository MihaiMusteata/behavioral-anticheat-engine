using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Domain.Behavior;
using BehavioralAnticheatEngine.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Repositories;

public sealed class DetectionRepository : IDetectionRepository
{
    private readonly AppDbContext _dbContext;

    public DetectionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<SessionDetection?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SessionDetections.FirstOrDefaultAsync(detection => detection.SessionId == sessionId, cancellationToken);
    }

    public Task<List<SessionDetection>> ListForSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken = default)
    {
        return _dbContext.SessionDetections
            .Where(detection => sessionIds.Contains(detection.SessionId))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SessionDetection detection, CancellationToken cancellationToken = default)
    {
        await _dbContext.SessionDetections.AddAsync(detection, cancellationToken);
    }

    public Task<List<SessionDetection>> ListNeedingRecoveryAsync(
        DateTimeOffset staleProcessingBeforeUtc,
        DateTimeOffset retryFailedBeforeUtc,
        int maxAttempts,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SessionDetections
            .Where(detection =>
                // stale = neither (re)queued nor started recently
                (detection.Status == SessionDetectionStatuses.Processing &&
                    detection.RequestedAtUtc < staleProcessingBeforeUtc &&
                    (detection.StartedAtUtc == null || detection.StartedAtUtc < staleProcessingBeforeUtc)) ||
                (detection.Status == SessionDetectionStatuses.Failed &&
                    detection.Attempts < maxAttempts &&
                    detection.CompletedAtUtc < retryFailedBeforeUtc))
            .OrderBy(detection => detection.RequestedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<(Guid SessionId, Guid ExamScheduleId, DateTimeOffset SubmittedAtUtc)>> ListSubmittedWithoutDetectionAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.ExamSessions
            .AsNoTracking()
            .Where(session => session.Status == ExamSessionStatuses.Submitted &&
                !_dbContext.SessionDetections.Any(detection => detection.SessionId == session.Id))
            .OrderBy(session => session.SubmittedAtUtc)
            .Take(limit)
            .Select(session => new { session.Id, session.ExamScheduleId, session.SubmittedAtUtc, session.LastActivityAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => (row.Id, row.ExamScheduleId, row.SubmittedAtUtc ?? row.LastActivityAtUtc))
            .ToList();
    }
}
