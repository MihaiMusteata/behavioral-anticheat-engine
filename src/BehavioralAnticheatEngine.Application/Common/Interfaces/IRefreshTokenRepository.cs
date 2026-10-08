using BehavioralAnticheatEngine.Domain.Identity;

namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenIdAsync(string tokenId, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}
