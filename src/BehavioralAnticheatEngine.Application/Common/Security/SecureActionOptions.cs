namespace BehavioralAnticheatEngine.Application.Common.Security;

public sealed record SecureActionOptions
{
    public const string SectionName = "SecureSessionActions";

    public int AcceptedClockSkewSeconds { get; init; } = 120;

    public int NonceTtlSeconds { get; init; } = 600;

    public int MaxEncryptedDataLength { get; init; } = 20_000;
}
