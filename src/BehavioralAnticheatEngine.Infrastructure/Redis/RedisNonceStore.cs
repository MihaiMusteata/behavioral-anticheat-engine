using BehavioralAnticheatEngine.Application.Common.Interfaces;
using StackExchange.Redis;

namespace BehavioralAnticheatEngine.Infrastructure.Redis;

public sealed class RedisNonceStore : INonceStore
{
    private readonly IDatabase _database;

    public RedisNonceStore(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public Task<bool> TryMarkNonceAsync(Guid sessionId, string nonce, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        return _database.StringSetAsync($"exam-session:{sessionId:N}:nonce:{nonce}", "1", ttl, When.NotExists);
    }
}
