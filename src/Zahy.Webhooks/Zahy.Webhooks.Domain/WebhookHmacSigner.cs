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

    /// <summary>
    /// Signature + replay-window verification. The constant-time signature compare always runs (the
    /// freshness check never short-circuits it), then the timestamp is checked against an injected
    /// "now": a captured-but-stale payload is rejected even when its signature is otherwise valid.
    /// </summary>
    public static WebhookVerificationOutcome VerifyWithFreshness(
        string secret,
        string payloadJson,
        long unixTimestamp,
        string signatureHeader,
        long nowUnixSeconds,
        int freshnessWindowSeconds)
    {
        var signatureValid = Verify(secret, payloadJson, unixTimestamp, signatureHeader);
        if (!signatureValid)
        {
            return WebhookVerificationOutcome.InvalidSignature;
        }

        if (Math.Abs(nowUnixSeconds - unixTimestamp) > freshnessWindowSeconds)
        {
            return WebhookVerificationOutcome.Stale;
        }

        return WebhookVerificationOutcome.Valid;
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

/// <summary>Outcome of a freshness-aware HMAC verification.</summary>
public enum WebhookVerificationOutcome
{
    /// <summary>Signature did not match the secret over the signed payload.</summary>
    InvalidSignature = 0,

    /// <summary>Signature matched but the timestamp is outside the replay window.</summary>
    Stale = 1,

    /// <summary>Signature matched and the timestamp is fresh.</summary>
    Valid = 2
}
