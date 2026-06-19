using System;
using System.Security.Cryptography;
using System.Text;

namespace Zahy.Webhooks;

public static class WebhookHmacSigner
{
    public const string SignaturePrefix = "sha256=";

    public static WebhookSignatureResult Sign(string secret, string payloadJson, DateTimeOffset? timestamp = null)
    {
        var unixSeconds = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var signedPayload = $"{unixSeconds}.{payloadJson}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
        var signature = SignaturePrefix + Convert.ToHexString(hash).ToLowerInvariant();

        return new WebhookSignatureResult(unixSeconds, signature);
    }

    public static bool Verify(string secret, string payloadJson, long unixTimestamp, string signatureHeader)
    {
        var expected = Sign(secret, payloadJson, DateTimeOffset.FromUnixTimeSeconds(unixTimestamp));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected.Signature),
            Encoding.UTF8.GetBytes(NormalizeSignature(signatureHeader)));
    }

    private static string NormalizeSignature(string signatureHeader)
    {
        if (signatureHeader.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return SignaturePrefix + signatureHeader[SignaturePrefix.Length..].ToLowerInvariant();
        }

        return SignaturePrefix + signatureHeader.ToLowerInvariant();
    }
}

public readonly record struct WebhookSignatureResult(long UnixTimestamp, string Signature);
