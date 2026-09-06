using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Dashboard.Contracts;
using StackExchange.Redis;

namespace BehavioralAnticheatEngine.Infrastructure.Redis;

public sealed class RedisDashboardEventBus : IDashboardEventBus
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;

    public RedisDashboardEventBus(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Task PublishAsync(DashboardLiveEvent liveEvent, CancellationToken cancellationToken = default)
    {
        return _redis.GetSubscriber().PublishAsync(
            RedisChannel.Literal(ChannelName(liveEvent.ExamScheduleId)),
            JsonSerializer.Serialize(liveEvent, WebJsonOptions));
    }

    public async IAsyncEnumerable<DashboardLiveEvent> SubscribeAsync(
        Guid examScheduleId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var subscriber = _redis.GetSubscriber();
        var redisChannel = RedisChannel.Literal(ChannelName(examScheduleId));
        var channel = Channel.CreateUnbounded<DashboardLiveEvent>();
        var queue = await subscriber.SubscribeAsync(redisChannel);
        queue.OnMessage(message =>
        {
            var liveEvent = JsonSerializer.Deserialize<DashboardLiveEvent>(message.Message.ToString(), WebJsonOptions);
            if (liveEvent is not null)
            {
                channel.Writer.TryWrite(liveEvent);
            }
        });

        try
        {
            await foreach (var liveEvent in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return liveEvent;
            }
        }
        finally
        {
            await queue.UnsubscribeAsync();
            channel.Writer.TryComplete();
        }
    }

    private static string ChannelName(Guid examScheduleId) => $"dashboard:exam:{examScheduleId:N}";
}
