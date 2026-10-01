namespace BehavioralAnticheatEngine.Application.Common.Contracts;

public sealed record SecureActionEnvelope(
    Guid SessionId,
    long Seq,
    DateTimeOffset Timestamp,
    string Nonce,
    string Data,
    string Signature);
