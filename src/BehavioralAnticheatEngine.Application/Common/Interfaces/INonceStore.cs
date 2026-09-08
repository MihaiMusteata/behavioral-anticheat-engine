namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface INonceStore
{
    Task<bool> TryMarkNonceAsync(Guid sessionId, string nonce, TimeSpan ttl, CancellationToken cancellationToken = default);
}
