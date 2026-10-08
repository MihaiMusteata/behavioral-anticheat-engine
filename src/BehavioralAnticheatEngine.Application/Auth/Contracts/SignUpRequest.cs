namespace BehavioralAnticheatEngine.Application.Auth.Contracts;

public sealed record SignUpRequest(string Email, string Password, string? Role = null);
