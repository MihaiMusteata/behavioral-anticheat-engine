namespace BehavioralAnticheatEngine.Application.Auth.Contracts;

public sealed record UserProfileResponse(Guid UserId, string Email, string Role);
