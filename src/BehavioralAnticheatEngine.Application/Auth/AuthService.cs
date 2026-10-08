using System.Net.Mail;
using BehavioralAnticheatEngine.Application.Auth.Contracts;
using BehavioralAnticheatEngine.Application.Common.Exceptions;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Observability;
using BehavioralAnticheatEngine.Domain.Identity;

namespace BehavioralAnticheatEngine.Application.Auth;

public sealed class AuthService : IAuthService
{
    private const int MinimumPasswordLength = 8;

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<AuthResponse> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken = default)
    {
        var email = ValidateAndNormalizeEmail(request.Email);
        ValidatePassword(request.Password);
        var role = ValidateRole(request.Role);

        if (await _users.ExistsByNormalizedEmailAsync(email.NormalizedValue, cancellationToken))
        {
            throw new ApplicationConflictException("An account with this email already exists.");
        }

        var now = _timeProvider.GetUtcNow();
        var user = User.Create(email.Email, email.NormalizedValue, _passwordHasher.Hash(request.Password), now, role);

        await _users.AddAsync(user, cancellationToken);

        return await IssueTokensAsync(user, now, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = ValidateAndNormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ApplicationValidationException("Password is required.");
        }

        var user = await _users.GetByNormalizedEmailAsync(email.NormalizedValue, cancellationToken);

        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            AppMetrics.AuthAttempts.Add(1, new KeyValuePair<string, object?>("outcome", "failure"));
            throw new ApplicationUnauthorizedException("Invalid email or password.");
        }

        var now = _timeProvider.GetUtcNow();
        user.RecordLogin(now);
        AppMetrics.AuthAttempts.Add(1, new KeyValuePair<string, object?>("outcome", "success"));

        return await IssueTokensAsync(user, now, cancellationToken);
    }

    public async Task<AuthResponse> JoinAsGuestAsync(string participantName, CancellationToken cancellationToken = default)
    {
        var trimmedName = (participantName ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
        {
            throw new ApplicationValidationException("Participation name is required.");
        }

        if (trimmedName.Length > 120)
        {
            throw new ApplicationValidationException("Participation name is too long.");
        }

        var now = _timeProvider.GetUtcNow();
        var syntheticEmail = $"guest-{Guid.NewGuid():N}@guest.local";
        var throwawayPassword = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var user = User.Create(
            syntheticEmail,
            syntheticEmail.ToUpperInvariant(),
            _passwordHasher.Hash(throwawayPassword),
            now,
            UserRoles.Student);

        await _users.AddAsync(user, cancellationToken);

        return await IssueTokensAsync(user, now, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new ApplicationValidationException("Refresh token is required.");
        }

        var validatedToken = _jwtTokenService.ValidateRefreshToken(request.RefreshToken);
        var storedToken = await _refreshTokens.GetByTokenIdAsync(validatedToken.TokenId, cancellationToken);

        if (storedToken is null ||
            storedToken.UserId != validatedToken.UserId ||
            !_jwtTokenService.VerifyTokenHash(request.RefreshToken, storedToken.TokenHash))
        {
            throw new ApplicationUnauthorizedException("Invalid refresh token.");
        }

        var now = _timeProvider.GetUtcNow();

        if (!storedToken.IsActive(now))
        {
            throw new ApplicationUnauthorizedException("Refresh token is no longer active.");
        }

        var user = await _users.GetByIdAsync(validatedToken.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new ApplicationUnauthorizedException("Invalid refresh token.");
        }

        var tokenPair = _jwtTokenService.CreateTokenPair(user, now);
        storedToken.Revoke(now, tokenPair.RefreshTokenId);

        await _refreshTokens.AddAsync(
            RefreshToken.Create(
                user.Id,
                tokenPair.RefreshTokenId,
                _jwtTokenService.HashToken(tokenPair.RefreshToken),
                now,
                tokenPair.RefreshTokenExpiresAtUtc),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToAuthResponse(user, tokenPair);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, DateTimeOffset issuedAtUtc, CancellationToken cancellationToken)
    {
        var tokenPair = _jwtTokenService.CreateTokenPair(user, issuedAtUtc);

        await _refreshTokens.AddAsync(
            RefreshToken.Create(
                user.Id,
                tokenPair.RefreshTokenId,
                _jwtTokenService.HashToken(tokenPair.RefreshToken),
                issuedAtUtc,
                tokenPair.RefreshTokenExpiresAtUtc),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToAuthResponse(user, tokenPair);
    }

    private static AuthResponse ToAuthResponse(User user, AuthTokenPair tokenPair)
    {
        return new AuthResponse(
            user.Id,
            user.Email,
            user.Role,
            tokenPair.AccessToken,
            tokenPair.AccessTokenExpiresAtUtc,
            tokenPair.RefreshToken,
            tokenPair.RefreshTokenExpiresAtUtc);
    }

    private static EmailNormalizationResult ValidateAndNormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ApplicationValidationException("Email is required.");
        }

        var trimmedEmail = email.Trim();

        try
        {
            var parsed = new MailAddress(trimmedEmail);

            if (!string.Equals(parsed.Address, trimmedEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new ApplicationValidationException("Email is invalid.");
            }
        }
        catch (FormatException)
        {
            throw new ApplicationValidationException("Email is invalid.");
        }

        return new EmailNormalizationResult(trimmedEmail, trimmedEmail.ToUpperInvariant());
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ApplicationValidationException("Password is required.");
        }

        if (password.Length < MinimumPasswordLength)
        {
            throw new ApplicationValidationException($"Password must be at least {MinimumPasswordLength} characters long.");
        }
    }

    private static string ValidateRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return UserRoles.Student;
        }

        var normalizedRole = role.Trim();
        if (!UserRoles.All.Contains(normalizedRole, StringComparer.Ordinal) || normalizedRole == UserRoles.Admin)
        {
            throw new ApplicationValidationException("Role must be Student or Proctor.");
        }

        return normalizedRole;
    }

    private sealed record EmailNormalizationResult(string Email, string NormalizedValue);
}
