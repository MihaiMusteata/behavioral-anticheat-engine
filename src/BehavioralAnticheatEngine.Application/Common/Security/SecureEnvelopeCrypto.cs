using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BehavioralAnticheatEngine.Application.Common.Security;

public static class SecureEnvelopeCrypto
{
    public const int AesGcmNonceSizeBytes = 12;
    private const int AesGcmTagSizeBytes = 16;
    private const int DerivedKeySizeBytes = 32;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string CreateSignatureMaterial(Guid sessionId, long seq, DateTimeOffset timestamp, string nonce, string data)
    {
        return string.Join(
            ".",
            sessionId.ToString("N"),
            seq.ToString(CultureInfo.InvariantCulture),
            timestamp.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            nonce,
            data);
    }

    public static bool VerifySignature(
        Guid sessionId,
        long seq,
        DateTimeOffset timestamp,
        string nonce,
        string data,
        string signature,
        string secret)
    {
        try
        {
            var expected = HMACSHA256.HashData(
                DeriveKey(secret, "signing"),
                Encoding.UTF8.GetBytes(CreateSignatureMaterial(sessionId, seq, timestamp, nonce, data)));
            var actual = Convert.FromBase64String(signature);

            return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool TryDecrypt(
        string nonce,
        string data,
        string secret,
        int maxDecryptedDataSizeBytes,
        out string plaintext,
        out string failureReason)
    {
        plaintext = string.Empty;
        failureReason = "unknown";

        try
        {
            if (!TryDecodeBase64Url(nonce, out var nonceBytes) || nonceBytes.Length != AesGcmNonceSizeBytes)
            {
                failureReason = "invalid_nonce";
                return false;
            }

            if (!TryDecodeBase64Url(data, out var encrypted) ||
                encrypted.Length <= AesGcmTagSizeBytes ||
                encrypted.Length > maxDecryptedDataSizeBytes + AesGcmTagSizeBytes)
            {
                failureReason = "invalid_encrypted_data";
                return false;
            }

            var ciphertext = encrypted[..^AesGcmTagSizeBytes];
            var tag = encrypted[^AesGcmTagSizeBytes..];
            var plaintextBytes = new byte[ciphertext.Length];
            var encryptionKey = DeriveKey(secret, "encryption");

            using var aes = new AesGcm(encryptionKey, AesGcmTagSizeBytes);
            aes.Decrypt(nonceBytes, ciphertext, tag, plaintextBytes);

            plaintext = StrictUtf8.GetString(plaintextBytes);
            failureReason = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            CryptographicException or
            FormatException or
            InvalidOperationException)
        {
            failureReason = exception is CryptographicException ? "aes_gcm_authentication_failed" : "invalid_event_encoding";
            return false;
        }
    }

    public static byte[] DeriveKey(string secret, string info)
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

    public static bool TryDecodeBase64Url(string value, out byte[] bytes)
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
}
