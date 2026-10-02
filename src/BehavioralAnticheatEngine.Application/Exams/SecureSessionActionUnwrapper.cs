using BehavioralAnticheatEngine.Application.Common.Contracts;
using BehavioralAnticheatEngine.Application.Common.Exceptions;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Common.Security;
using Microsoft.Extensions.Options;

namespace BehavioralAnticheatEngine.Application.Exams;

public sealed class SecureSessionActionUnwrapper : ISecureSessionActionUnwrapper
{
    private readonly IExamRepository _exams;
    private readonly IExamSessionSecretStore _sessionSecrets;
    private readonly INonceStore _nonces;
    private readonly SecureActionOptions _options;
    private readonly TimeProvider _timeProvider;

    public SecureSessionActionUnwrapper(
        IExamRepository exams,
        IExamSessionSecretStore sessionSecrets,
        INonceStore nonces,
        IOptions<SecureActionOptions> options,
        TimeProvider timeProvider)
    {
        _exams = exams;
        _sessionSecrets = sessionSecrets;
        _nonces = nonces;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<string> UnwrapAsync(Guid studentId, SecureActionEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (envelope.SessionId == Guid.Empty || envelope.Seq <= 0)
        {
            throw new ApplicationValidationException("Invalid request envelope.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Nonce) ||
            string.IsNullOrWhiteSpace(envelope.Data) ||
            string.IsNullOrWhiteSpace(envelope.Signature))
        {
            throw new ApplicationValidationException("Invalid request envelope.");
        }

        if (envelope.Nonce.Length > 32 ||
            envelope.Data.Length > _options.MaxEncryptedDataLength ||
            envelope.Signature.Length > 256)
        {
            throw new ApplicationValidationException("Invalid request envelope.");
        }

        var session = await _exams.GetSessionAsync(envelope.SessionId, cancellationToken);
        if (session is null || session.StudentId != studentId)
        {
            throw new ApplicationNotFoundException("Exam session was not found.");
        }

        var now = _timeProvider.GetUtcNow();
        var skew = TimeSpan.FromSeconds(_options.AcceptedClockSkewSeconds);
        if (envelope.Timestamp < now.Subtract(skew) || envelope.Timestamp > now.Add(skew))
        {
            throw new ApplicationUnauthorizedException("Request timestamp is outside the accepted window.");
        }

        var secret = await _sessionSecrets.GetSecretAsync(envelope.SessionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ApplicationUnauthorizedException("Session secret is unavailable.");
        }

        if (!SecureEnvelopeCrypto.VerifySignature(envelope.SessionId, envelope.Seq, envelope.Timestamp, envelope.Nonce, envelope.Data, envelope.Signature, secret))
        {
            throw new ApplicationUnauthorizedException("Request signature is invalid.");
        }

        var nonceAccepted = await _nonces.TryMarkNonceAsync(
            envelope.SessionId,
            envelope.Nonce,
            TimeSpan.FromSeconds(_options.NonceTtlSeconds),
            cancellationToken);
        if (!nonceAccepted)
        {
            throw new ApplicationUnauthorizedException("Request nonce was already used.");
        }

        if (!SecureEnvelopeCrypto.TryDecrypt(envelope.Nonce, envelope.Data, secret, _options.MaxEncryptedDataLength, out var plaintext, out _))
        {
            throw new ApplicationValidationException("Request payload could not be decrypted.");
        }

        return plaintext;
    }
}
