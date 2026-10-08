namespace BehavioralAnticheatEngine.Application.Common.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public JwtTokenOptions AccessToken { get; init; } = new();

    public JwtTokenOptions RefreshToken { get; init; } = new();
}
