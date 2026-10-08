namespace BehavioralAnticheatEngine.Application.Auth.Contracts;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string Role,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);
