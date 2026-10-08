namespace BehavioralAnticheatEngine.Infrastructure.Redis;

public sealed record RedisOptions
{
    public const string SectionName = "Redis";

    public string Configuration { get; init; } = "localhost:6379";
}
