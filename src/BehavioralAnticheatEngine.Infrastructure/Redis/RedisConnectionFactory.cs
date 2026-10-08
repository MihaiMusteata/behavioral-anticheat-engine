using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace BehavioralAnticheatEngine.Infrastructure.Redis;

public static class RedisConnectionFactory
{
    public static IConnectionMultiplexer Create(IConfiguration configuration)
    {
        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        return ConnectionMultiplexer.Connect(options.Configuration);
    }
}
