using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _dbContext;

    public RefreshTokenRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<RefreshToken?> GetByTokenIdAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        return _dbContext.RefreshTokens.FirstOrDefaultAsync(refreshToken => refreshToken.TokenId == tokenId, cancellationToken);
    }

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        await _dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
    }
}
