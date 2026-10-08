namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public sealed record AuthTokenPair(
    string AccessToken,
    string AccessTokenId,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    string RefreshTokenId,
    DateTimeOffset RefreshTokenExpiresAtUtc);
