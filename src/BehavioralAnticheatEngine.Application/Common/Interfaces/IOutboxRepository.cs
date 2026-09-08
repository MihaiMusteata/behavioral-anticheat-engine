using BehavioralAnticheatEngine.Domain.Behavior;

namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    Task<List<OutboxMessage>> GetPendingAsync(int take, CancellationToken cancellationToken = default);
}
