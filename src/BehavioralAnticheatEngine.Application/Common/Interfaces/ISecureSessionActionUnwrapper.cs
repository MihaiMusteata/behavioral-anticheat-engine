using BehavioralAnticheatEngine.Application.Common.Contracts;

namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface ISecureSessionActionUnwrapper
{
    Task<string> UnwrapAsync(Guid studentId, SecureActionEnvelope envelope, CancellationToken cancellationToken = default);
}
