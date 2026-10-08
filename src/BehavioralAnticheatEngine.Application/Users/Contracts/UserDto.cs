namespace BehavioralAnticheatEngine.Application.Users.Contracts;

public sealed record UserDto(Guid Id, string Email, string Role);
