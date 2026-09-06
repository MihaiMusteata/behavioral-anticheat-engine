using BehavioralAnticheatEngine.Application.Common.Interfaces;
using StackExchange.Redis;

namespace BehavioralAnticheatEngine.Infrastructure.Redis;

public sealed class RedisSequenceStore : ISequenceStore
{
    private static readonly LuaScript AdvanceSequenceScript = LuaScript.Prepare(
        """
        local current = redis.call('GET', @key)
        local nextSeq = tonumber(@seq)
        local largeGapThreshold = tonumber(@largeGapThreshold)
        local ttlSeconds = tonumber(@ttlSeconds)

        if not current then
          redis.call('SET', @key, nextSeq, 'EX', ttlSeconds)
          local gapDetected = 0
          local largeGap = 0
          if nextSeq > 1 then
            gapDetected = 1
          end
          if nextSeq > largeGapThreshold then
            largeGap = 1
          end
          return {1, gapDetected, largeGap, 0}
        end

        local currentSeq = tonumber(current)
        if nextSeq <= currentSeq then
          return {0, 0, 0, currentSeq}
        end

        redis.call('SET', @key, nextSeq, 'KEEPTTL')
        if redis.call('TTL', @key) < 0 then
          redis.call('EXPIRE', @key, ttlSeconds)
        end

        local gap = nextSeq - currentSeq
        local gapDetected = 0
        local largeGap = 0
        if gap > 1 then
          gapDetected = 1
        end
        if gap > largeGapThreshold then
          largeGap = 1
        end

        return {1, gapDetected, largeGap, currentSeq}
        """);

    private readonly IDatabase _database;

    public RedisSequenceStore(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task<SequenceValidationResult> TryAdvanceAsync(
        Guid sessionId,
        long seq,
        long largeGapThreshold,
        CancellationToken cancellationToken = default)
    {
        var result = (RedisResult[]?)await _database.ScriptEvaluateAsync(
            AdvanceSequenceScript,
            new { key = (RedisKey)$"exam-session:{sessionId:N}:seq", seq, largeGapThreshold, ttlSeconds = 86_400 });

        if (result is null || result.Length != 4)
        {
            return new SequenceValidationResult(false, false, false, 0);
        }

        return new SequenceValidationResult(
            (int)result[0] == 1,
            (int)result[1] == 1,
            (int)result[2] == 1,
            (long)result[3]);
    }
}
