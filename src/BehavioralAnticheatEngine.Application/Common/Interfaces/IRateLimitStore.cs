namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface IRateLimitStore
{
    Task<bool> TryConsumeAsync(string key, int amount, int limit, TimeSpan window, CancellationToken cancellationToken = default);
}
