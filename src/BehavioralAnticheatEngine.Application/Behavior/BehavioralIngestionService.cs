using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BehavioralAnticheatEngine.Application.Behavior.Contracts;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Observability;
using BehavioralAnticheatEngine.Domain.Exams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BehavioralAnticheatEngine.Application.Behavior;

public sealed class BehavioralIngestionService : IBehavioralIngestionService
{
    private const int AesGcmNonceSizeBytes = 12;
    private const int AesGcmTagSizeBytes = 16;
    private const int DerivedKeySizeBytes = 32;
    private const int MaxEventTypeLength = 128;
    private const int MaxDecryptedDataSizeBytes = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly IExamRepository _exams;
    private readonly IExamSessionSecretStore _sessionSecrets;
    private readonly INonceStore _nonces;
    private readonly ISequenceStore _sequences;
    private readonly IRateLimitStore _rateLimits;
    private readonly IBehavioralEventPublisher _publisher;
    private readonly BehavioralIngestionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BehavioralIngestionService> _logger;

    public BehavioralIngestionService(
        IExamRepository exams,
        IExamSessionSecretStore sessionSecrets,
        INonceStore nonces,
        ISequenceStore sequences,
        IRateLimitStore rateLimits,
        IBehavioralEventPublisher publisher,
        IOptions<BehavioralIngestionOptions> options,
        TimeProvider timeProvider,
        ILogger<BehavioralIngestionService> logger)
    {
        _exams = exams;
        _sessionSecrets = sessionSecrets;
        _nonces = nonces;
        _sequences = sequences;
        _rateLimits = rateLimits;
        _publisher = publisher;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<BehavioralEventBatchResult> IngestAsync(Guid studentId, BehavioralEventBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Events is null)
        {
            AppMetrics.BehavioralEventsIngested.Add(1, new("outcome", "rejected"), new("reason", "events_required"));
            return new BehavioralEventBatchResult(
                0,
                1,
                [new BehavioralEventResult(0, false, "events_required", null)]);
        }

        if (request.Events.Count == 0)
        {
            return new BehavioralEventBatchResult(0, 0, []);
        }

        if (request.Events.Count > _options.MaxBatchSize)
        {
            AppMetrics.BehavioralEventsIngested.Add(
                request.Events.Count,
                new("outcome", "rejected"),
                new("reason", "batch_too_large"));
            return new BehavioralEventBatchResult(
                0,
                request.Events.Count,
                request.Events
                    .Select(envelope => new BehavioralEventResult(envelope.Seq, false, "batch_too_large", null))
                    .ToArray());
        }

        var rateLimitAccepted = await _rateLimits.TryConsumeAsync(
            $"behavior-ingest:{studentId:N}",
            request.Events.Count,
            _options.MaxEventsPerMinute,
            TimeSpan.FromMinutes(1),
            cancellationToken);
        if (!rateLimitAccepted)
        {
            AppMetrics.RateLimitRejections.Add(1);
            AppMetrics.BehavioralEventsIngested.Add(
                request.Events.Count,
                new("outcome", "rejected"),
                new("reason", "rate_limited"));
            return new BehavioralEventBatchResult(
                0,
                request.Events.Count,
                request.Events.Select(envelope => new BehavioralEventResult(envelope.Seq, false, "rate_limited", null)).ToArray());
        }

        var results = new List<BehavioralEventResult>(request.Events.Count);
        foreach (var envelope in request.Events.OrderBy(candidate => candidate.Seq))
        {
            var result = await ValidateAndPublishAsync(studentId, envelope, cancellationToken);
            results.Add(result);
        }

        return new BehavioralEventBatchResult(
            results.Count(result => result.Accepted),
            results.Count(result => !result.Accepted),
            results);
    }

    private async Task<BehavioralEventResult> ValidateAndPublishAsync(
        Guid studentId,
        BehavioralEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (envelope.SessionId == Guid.Empty || envelope.Seq <= 0)
        {
            return Reject(envelope.Seq, "invalid_envelope");
        }

        if (string.IsNullOrWhiteSpace(envelope.Nonce) ||
            string.IsNullOrWhiteSpace(envelope.Data) ||
            string.IsNullOrWhiteSpace(envelope.Signature))
        {
            return Reject(envelope.Seq, "missing_required_field");
        }

        if (envelope.Nonce.Length > 32 ||
            envelope.Data.Length > _options.MaxEncryptedDataLength ||
            envelope.Signature.Length > 256)
        {
            return Reject(envelope.Seq, "field_too_large");
        }

        var session = await _exams.GetSessionAsync(envelope.SessionId, cancellationToken);
        if (session is null || session.StudentId != studentId)
        {
            return Reject(envelope.Seq, "session_not_found");
        }

        if (session.Status != ExamSessionStatuses.Active)
        {
            return Reject(envelope.Seq, "session_not_active");
        }

        var now = _timeProvider.GetUtcNow();
        var skew = TimeSpan.FromSeconds(_options.AcceptedClockSkewSeconds);
        if (envelope.Timestamp < now.Subtract(skew) || envelope.Timestamp > now.Add(skew))
        {
            await PublishValidationIncidentAsync(session, envelope, "timestamp_outside_window", now, cancellationToken);
            return Reject(envelope.Seq, "timestamp_outside_window");
        }

        var secret = await _sessionSecrets.GetSecretAsync(envelope.SessionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(secret))
        {
            return Reject(envelope.Seq, "session_secret_missing");
        }

        if (!VerifySignature(envelope, secret))
        {
            await PublishValidationIncidentAsync(session, envelope, "invalid_signature", now, cancellationToken);
            return Reject(envelope.Seq, "invalid_signature");
        }

        if (!TryDecodeBase64Url(envelope.Nonce, out var decodedNonce) || decodedNonce.Length != AesGcmNonceSizeBytes)
        {
            await PublishValidationIncidentAsync(session, envelope, "invalid_nonce", now, cancellationToken);
            return Reject(envelope.Seq, "invalid_nonce");
        }

        if (!TryDecodeBase64Url(envelope.Data, out var decodedData) ||
            decodedData.Length <= AesGcmTagSizeBytes ||
            decodedData.Length > MaxDecryptedDataSizeBytes + AesGcmTagSizeBytes)
        {
            _logger.LogError(
                "Behavioral encrypted data encoding was invalid after a valid HMAC for session {SessionId}, sequence {Seq}.",
                envelope.SessionId,
                envelope.Seq);
            await PublishValidationIncidentAsync(session, envelope, "data_decryption_failed", now, cancellationToken);
            return Reject(envelope.Seq, "data_decryption_failed");
        }

        var nonceAccepted = await _nonces.TryMarkNonceAsync(
            envelope.SessionId,
            envelope.Nonce,
            TimeSpan.FromSeconds(_options.NonceTtlSeconds),
            cancellationToken);
        if (!nonceAccepted)
        {
            await PublishValidationIncidentAsync(session, envelope, "replayed_nonce", now, cancellationToken);
            return Reject(envelope.Seq, "replayed_nonce");
        }

        var sequence = await _sequences.TryAdvanceAsync(
            envelope.SessionId,
            envelope.Seq,
            _options.LargeSequenceGapThreshold,
            cancellationToken);
        if (!sequence.Accepted)
        {
            await PublishValidationIncidentAsync(session, envelope, "sequence_not_monotonic", now, cancellationToken);
            return Reject(envelope.Seq, "sequence_not_monotonic");
        }

        var validationFlags = new List<string>(2);
        if (sequence.GapDetected)
        {
            validationFlags.Add("seq_gap");
        }

        if (sequence.LargeGap)
        {
            validationFlags.Add("large_seq_gap");
        }

        var flags = string.Join(',', validationFlags);
        var decrypted = TryDecryptEventData(envelope, secret, out var decryptionFailure);
        if (decrypted is null)
        {
            _logger.LogError(
                "Behavioral data decryption/validation failed after a valid HMAC for session {SessionId}, sequence {Seq}. Failure: {Failure}.",
                envelope.SessionId,
                envelope.Seq,
                decryptionFailure);
            await PublishValidationIncidentAsync(session, envelope, "data_decryption_failed", now, cancellationToken);
            return Reject(envelope.Seq, "data_decryption_failed");
        }

        var validated = new ValidatedBehavioralEvent(
            envelope.SessionId,
            session.ExamScheduleId,
            session.StudentId,
            envelope.Seq,
            envelope.Timestamp.ToUniversalTime(),
            envelope.Nonce,
            decrypted.QuestionId,
            decrypted.EventType,
            decrypted.PayloadJson,
            envelope.Signature,
            flags,
            now);

        await _publisher.PublishAsync(validated, cancellationToken);
        if (sequence.GapDetected)
        {
            await PublishSystemEventAsync(
                session,
                "event_sequence_gap",
                new
                {
                    observedSeq = envelope.Seq,
                    previousSeq = sequence.PreviousSeq,
                    sequenceJump = envelope.Seq - sequence.PreviousSeq,
                    missingCount = envelope.Seq - sequence.PreviousSeq - 1,
                    largeGap = sequence.LargeGap,
                    largeGapThreshold = _options.LargeSequenceGapThreshold
                },
                now,
                cancellationToken);
        }

        AppMetrics.BehavioralEventsIngested.Add(1, new("outcome", "accepted"), new("reason", "none"));
        return new BehavioralEventResult(envelope.Seq, true, null, flags);
    }

    public static string CreateSignatureMaterial(BehavioralEventEnvelope envelope)
    {
        return string.Join(
            ".",
            envelope.SessionId.ToString("N"),
            envelope.Seq.ToString(System.Globalization.CultureInfo.InvariantCulture),
            envelope.Timestamp.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            envelope.Nonce,
            envelope.Data);
    }

    private static bool VerifySignature(BehavioralEventEnvelope envelope, string secret)
    {
        try
        {
            var expected = HMACSHA256.HashData(
                DeriveKey(secret, "signing"),
                Encoding.UTF8.GetBytes(CreateSignatureMaterial(envelope)));
            var actual = Convert.FromBase64String(envelope.Signature);

            return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static DecryptedBehavioralEventData? TryDecryptEventData(
        BehavioralEventEnvelope envelope,
        string secret,
        out string failureReason)
    {
        failureReason = "unknown";

        try
        {
            if (!TryDecodeBase64Url(envelope.Nonce, out var nonce) || nonce.Length != AesGcmNonceSizeBytes)
            {
                failureReason = "invalid_nonce";
                return null;
            }

            if (!TryDecodeBase64Url(envelope.Data, out var encrypted) ||
                encrypted.Length <= AesGcmTagSizeBytes ||
                encrypted.Length > MaxDecryptedDataSizeBytes + AesGcmTagSizeBytes)
            {
                failureReason = "invalid_encrypted_data";
                return null;
            }

            var ciphertext = encrypted[..^AesGcmTagSizeBytes];
            var tag = encrypted[^AesGcmTagSizeBytes..];
            var plaintext = new byte[ciphertext.Length];
            var encryptionKey = DeriveKey(secret, "encryption");

            using var aes = new AesGcm(encryptionKey, AesGcmTagSizeBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            var payloadJson = StrictUtf8.GetString(plaintext);
            using var document = JsonDocument.Parse(
                payloadJson,
                new JsonDocumentOptions { MaxDepth = 32, CommentHandling = JsonCommentHandling.Disallow });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                failureReason = "event_data_must_be_an_object";
                return null;
            }

            if (!document.RootElement.TryGetProperty("eventType", out var eventTypeElement) ||
                eventTypeElement.ValueKind != JsonValueKind.String ||
                !TryNormalizeEventType(eventTypeElement.GetString(), out var eventType))
            {
                failureReason = "invalid_event_type";
                return null;
            }

            Guid? questionId = null;
            if (document.RootElement.TryGetProperty("questionId", out var questionIdElement) &&
                questionIdElement.ValueKind != JsonValueKind.Null)
            {
                if (questionIdElement.ValueKind != JsonValueKind.String ||
                    !Guid.TryParse(questionIdElement.GetString(), out var parsedQuestionId) ||
                    parsedQuestionId == Guid.Empty)
                {
                    failureReason = "invalid_question_id";
                    return null;
                }

                questionId = parsedQuestionId;
            }

            failureReason = string.Empty;
            return new DecryptedBehavioralEventData(eventType, questionId, payloadJson);
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            CryptographicException or
            FormatException or
            InvalidOperationException or
            JsonException)
        {
            failureReason = exception switch
            {
                CryptographicException => "aes_gcm_authentication_failed",
                JsonException => "invalid_event_json",
                _ => "invalid_event_encoding"
            };
            return null;
        }
    }

    private static bool TryNormalizeEventType(string? value, out string eventType)
    {
        eventType = value?.Trim() ?? string.Empty;
        if (eventType.Length == 0 ||
            eventType.Length > MaxEventTypeLength ||
            !string.Equals(value, eventType, StringComparison.Ordinal))
        {
            return false;
        }

        return eventType.All(character =>
            character is >= 'a' and <= 'z' or
                >= 'A' and <= 'Z' or
                >= '0' and <= '9' or
                '_' or '-' or '.');
    }

    private async Task PublishValidationIncidentAsync(
        ExamSession session,
        BehavioralEventEnvelope envelope,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await PublishSystemEventAsync(
            session,
            "event_validation_failed",
            new
            {
                reason,
                clientSeq = envelope.Seq,
                timestampUtc = envelope.Timestamp.ToUniversalTime(),
                noncePresent = !string.IsNullOrWhiteSpace(envelope.Nonce),
                dataPresent = !string.IsNullOrWhiteSpace(envelope.Data)
            },
            now,
            cancellationToken);
    }

    private async Task PublishSystemEventAsync(
        ExamSession session,
        string eventType,
        object payload,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var serverSeq = -RandomNumberGenerator.GetInt32(1, int.MaxValue);
        var payloadJson = JsonSerializer.Serialize(payload);
        var systemEvent = new ValidatedBehavioralEvent(
            session.Id,
            session.ExamScheduleId,
            session.StudentId,
            serverSeq,
            now,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(AesGcmNonceSizeBytes)),
            null,
            eventType,
            payloadJson,
            "server",
            "server_generated",
            now);

        await _publisher.PublishAsync(systemEvent, cancellationToken);
    }

    private static byte[] DeriveKey(string secret, string info)
    {
        var inputKeyingMaterial = DecodeSessionSecret(secret);
        using var extract = new HMACSHA256(new byte[32]);
        var pseudorandomKey = extract.ComputeHash(inputKeyingMaterial);
        using var expand = new HMACSHA256(pseudorandomKey);

        var infoBytes = Encoding.UTF8.GetBytes(info);
        var blockInput = new byte[infoBytes.Length + 1];
        Buffer.BlockCopy(infoBytes, 0, blockInput, 0, infoBytes.Length);
        blockInput[^1] = 1;

        return expand.ComputeHash(blockInput)[..DerivedKeySizeBytes];
    }

    private static byte[] DecodeSessionSecret(string secret)
    {
        try
        {
            return Convert.FromBase64String(secret);
        }
        catch (FormatException)
        {
            return Encoding.UTF8.GetBytes(secret);
        }
    }

    private static bool TryDecodeBase64Url(string value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length % 4 == 1 ||
            value.Any(character => !(
                character is >= 'a' and <= 'z' or
                    >= 'A' and <= 'Z' or
                    >= '0' and <= '9' or
                    '-' or '_')))
        {
            return false;
        }

        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding > 0)
        {
            normalized = normalized.PadRight(normalized.Length + 4 - padding, '=');
        }

        try
        {
            bytes = Convert.FromBase64String(normalized);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static BehavioralEventResult Reject(long seq, string reason)
    {
        AppMetrics.BehavioralEventsIngested.Add(1, new("outcome", "rejected"), new("reason", reason));
        return new BehavioralEventResult(seq, false, reason, null);
    }

    private sealed record DecryptedBehavioralEventData(string EventType, Guid? QuestionId, string PayloadJson);
}
