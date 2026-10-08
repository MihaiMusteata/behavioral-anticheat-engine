using System.Text.Json;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Dashboard.Contracts;
using StackExchange.Redis;

namespace BehavioralAnticheatEngine.Infrastructure.Redis;

public sealed class RedisLiveExamStateStore : ILiveExamStateStore
{
    private readonly IDatabase _database;

    public RedisLiveExamStateStore(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task UpsertAsync(LiveSessionState state, CancellationToken cancellationToken = default)
    {
        var key = Key(state.ExamScheduleId);
        await _database.HashSetAsync(key, state.SessionId.ToString("N"), JsonSerializer.Serialize(state));
        await _database.KeyExpireAsync(key, TimeSpan.FromHours(24));
    }

    public async Task<IReadOnlyCollection<LiveSessionState>> ListByExamAsync(Guid examScheduleId, CancellationToken cancellationToken = default)
    {
        var entries = await _database.HashGetAllAsync(Key(examScheduleId));
        var states = new List<LiveSessionState>(entries.Length);

        foreach (var entry in entries)
        {
            if (!entry.Value.HasValue)
            {
                continue;
            }

            var state = JsonSerializer.Deserialize<LiveSessionState>(entry.Value.ToString());
            if (state is not null)
            {
                states.Add(state);
            }
        }

        return states;
    }

    private static RedisKey Key(Guid examScheduleId) => $"live:exam:{examScheduleId:N}:sessions";
}
