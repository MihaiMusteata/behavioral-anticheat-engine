using BehavioralAnticheatEngine.Domain.Behavior;

namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface IDetectionRepository
{
    Task<SessionDetection?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<List<SessionDetection>> ListForSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken = default);

    Task AddAsync(SessionDetection detection, CancellationToken cancellationToken = default);

    /// <summary>Processing for too long (lost queue message / crashed worker) or failed and due for a retry.</summary>
    Task<List<SessionDetection>> ListNeedingRecoveryAsync(
        DateTimeOffset staleProcessingBeforeUtc,
        DateTimeOffset retryFailedBeforeUtc,
        int maxAttempts,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Submitted sessions that never got a detection row (e.g. submitted before the AI scoring existed).</summary>
    Task<List<(Guid SessionId, Guid ExamScheduleId, DateTimeOffset SubmittedAtUtc)>> ListSubmittedWithoutDetectionAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
