using BehavioralAnticheatEngine.Domain.Identity;

namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface IJwtTokenService
{
    AuthTokenPair CreateTokenPair(User user, DateTimeOffset issuedAtUtc);

    ValidatedRefreshToken ValidateRefreshToken(string refreshToken);

    string HashToken(string token);

    bool VerifyTokenHash(string token, string tokenHash);
}
