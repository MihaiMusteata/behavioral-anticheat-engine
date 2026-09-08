namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface IExamSessionSecretStore
{
    Task StoreSecretAsync(Guid sessionId, string secret, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task<string?> GetSecretAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
