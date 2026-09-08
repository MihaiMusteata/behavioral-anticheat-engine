namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public sealed record ValidatedRefreshToken(Guid UserId, string TokenId);
