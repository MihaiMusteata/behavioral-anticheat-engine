using BehavioralAnticheatEngine.Application.Common.Interfaces;
using StackExchange.Redis;

namespace BehavioralAnticheatEngine.Infrastructure.Redis;

public sealed class RedisExamSessionSecretStore : IExamSessionSecretStore
{
    private readonly IDatabase _database;

    public RedisExamSessionSecretStore(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public Task StoreSecretAsync(Guid sessionId, string secret, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        return _database.StringSetAsync(Key(sessionId), secret, ttl);
    }

    public async Task<string?> GetSecretAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(Key(sessionId));
        return value.HasValue ? value.ToString() : null;
    }

    private static RedisKey Key(Guid sessionId) => $"exam-session:{sessionId:N}:secret";
}
