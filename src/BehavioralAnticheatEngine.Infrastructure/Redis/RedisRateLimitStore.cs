using BehavioralAnticheatEngine.Application.Common.Interfaces;
using StackExchange.Redis;

namespace BehavioralAnticheatEngine.Infrastructure.Redis;

public sealed class RedisRateLimitStore : IRateLimitStore
{
    private readonly IDatabase _database;

    public RedisRateLimitStore(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task<bool> TryConsumeAsync(string key, int amount, int limit, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var redisKey = (RedisKey)$"rate-limit:{key}";
        var current = await _database.StringIncrementAsync(redisKey, amount);

        if (current == amount)
        {
            await _database.KeyExpireAsync(redisKey, window);
        }

        return current <= limit;
    }
}
