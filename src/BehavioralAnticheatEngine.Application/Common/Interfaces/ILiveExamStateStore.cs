using BehavioralAnticheatEngine.Application.Dashboard.Contracts;

namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface ILiveExamStateStore
{
    Task UpsertAsync(LiveSessionState state, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LiveSessionState>> ListByExamAsync(Guid examScheduleId, CancellationToken cancellationToken = default);
}
