using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BehavioralAnticheatEngine.Application.Common.Exceptions;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Common.Security;
using BehavioralAnticheatEngine.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BehavioralAnticheatEngine.Infrastructure.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    private const string TokenUseClaim = "token_use";
    private const string AccessTokenUse = "access";
    private const string RefreshTokenUse = "refresh";

    private readonly JwtOptions _options;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public AuthTokenPair CreateTokenPair(User user, DateTimeOffset issuedAtUtc)
    {
        var accessToken = CreateToken(user, _options.AccessToken, AccessTokenUse, issuedAtUtc);
        var refreshToken = CreateToken(user, _options.RefreshToken, RefreshTokenUse, issuedAtUtc);

        return new AuthTokenPair(
            accessToken.Token,
            accessToken.TokenId,
            accessToken.ExpiresAtUtc,
            refreshToken.Token,
            refreshToken.TokenId,
            refreshToken.ExpiresAtUtc);
    }

    public ValidatedRefreshToken ValidateRefreshToken(string refreshToken)
    {
        try
        {
            var principal = _tokenHandler.ValidateToken(
                refreshToken,
                CreateValidationParameters(_options.RefreshToken),
                out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !string.Equals(jwtToken.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
            {
                throw new ApplicationUnauthorizedException("Invalid refresh token.");
            }

            var tokenUse = principal.FindFirst(TokenUseClaim)?.Value;

            if (tokenUse != RefreshTokenUse)
            {
                throw new ApplicationUnauthorizedException("Invalid refresh token.");
            }

            var userIdValue =
                principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var tokenId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(tokenId))
            {
                throw new ApplicationUnauthorizedException("Invalid refresh token.");
            }

            return new ValidatedRefreshToken(userId, tokenId);
        }
        catch (SecurityTokenException)
        {
            throw new ApplicationUnauthorizedException("Invalid refresh token.");
        }
        catch (ArgumentException)
        {
            throw new ApplicationUnauthorizedException("Invalid refresh token.");
        }
    }

    public string HashToken(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public bool VerifyTokenHash(string token, string tokenHash)
    {
        try
        {
            var actualHash = Convert.FromBase64String(HashToken(token));
            var expectedHash = Convert.FromBase64String(tokenHash);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private CreatedToken CreateToken(
        User user,
        JwtTokenOptions tokenOptions,
        string tokenUse,
        DateTimeOffset issuedAtUtc)
    {
        ValidateTokenOptions(tokenOptions);

        var tokenId = Guid.NewGuid().ToString("N");
        var expiresAtUtc = issuedAtUtc.AddMinutes(tokenOptions.LifetimeMinutes);
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, tokenId),
            new Claim(TokenUseClaim, tokenUse)
        };
        var jwt = new JwtSecurityToken(
            tokenOptions.Issuer,
            tokenOptions.Audience,
            claims,
            issuedAtUtc.UtcDateTime,
            expiresAtUtc.UtcDateTime,
            signingCredentials);

        return new CreatedToken(_tokenHandler.WriteToken(jwt), tokenId, expiresAtUtc);
    }

    private static TokenValidationParameters CreateValidationParameters(JwtTokenOptions tokenOptions)
    {
        ValidateTokenOptions(tokenOptions);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = tokenOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenOptions.SigningKey)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(tokenOptions.ClockSkewSeconds),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    }

    private static void ValidateTokenOptions(JwtTokenOptions tokenOptions)
    {
        if (string.IsNullOrWhiteSpace(tokenOptions.Issuer) ||
            string.IsNullOrWhiteSpace(tokenOptions.Audience) ||
            string.IsNullOrWhiteSpace(tokenOptions.SigningKey) ||
            tokenOptions.SigningKey.Length < 32 ||
            tokenOptions.LifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("JWT configuration is missing or invalid.");
        }
    }

    private sealed record CreatedToken(string Token, string TokenId, DateTimeOffset ExpiresAtUtc);
}
