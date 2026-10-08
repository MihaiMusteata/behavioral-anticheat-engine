using BehavioralAnticheatEngine.Application.Dashboard.Contracts;

namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface IDashboardEventBus
{
    Task PublishAsync(DashboardLiveEvent liveEvent, CancellationToken cancellationToken = default);

    IAsyncEnumerable<DashboardLiveEvent> SubscribeAsync(Guid examScheduleId, CancellationToken cancellationToken = default);
}
