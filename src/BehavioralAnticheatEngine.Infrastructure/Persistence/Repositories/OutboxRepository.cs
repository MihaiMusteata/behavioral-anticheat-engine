using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Domain.Behavior;
using Microsoft.EntityFrameworkCore;

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _dbContext;

    public OutboxRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public Task<List<OutboxMessage>> GetPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        return _dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
