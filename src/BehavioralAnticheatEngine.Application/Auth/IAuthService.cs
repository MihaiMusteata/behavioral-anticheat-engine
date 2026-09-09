using BehavioralAnticheatEngine.Application.Auth.Contracts;

namespace BehavioralAnticheatEngine.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> JoinAsGuestAsync(string participantName, CancellationToken cancellationToken = default);
}
