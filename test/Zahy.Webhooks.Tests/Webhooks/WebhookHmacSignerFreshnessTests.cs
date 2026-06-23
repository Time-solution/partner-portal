using System;
using Shouldly;
using Xunit;

namespace Zahy.Webhooks;

public class WebhookHmacSignerFreshnessTests
{
    private const string Secret = "freshness-secret";
    private const string Payload = """{"orderId":"abc","totalAmount":42}""";
    private const long SignedAt = 1_700_000_000L;
    private const int Window = 300;

    private static string Sign(string secret = Secret, string payload = Payload, long ts = SignedAt) =>
        WebhookHmacSigner.Sign(secret, payload, DateTimeOffset.FromUnixTimeSeconds(ts)).Signature;

    [Fact]
    public void Fresh_Matching_Signature_Is_Valid()
    {
        var outcome = WebhookHmacSigner.VerifyWithFreshness(
            Secret, Payload, SignedAt, Sign(), nowUnixSeconds: SignedAt + 10, freshnessWindowSeconds: Window);

        outcome.ShouldBe(WebhookVerificationOutcome.Valid);
    }

    [Fact]
    public void Tampered_Payload_Is_InvalidSignature_Even_When_Fresh()
    {
        // Constant-time signature compare still runs and fails before freshness can pass it.
        var outcome = WebhookHmacSigner.VerifyWithFreshness(
            Secret, Payload + "X", SignedAt, Sign(), nowUnixSeconds: SignedAt, freshnessWindowSeconds: Window);

        outcome.ShouldBe(WebhookVerificationOutcome.InvalidSignature);
    }

    [Fact]
    public void Foreign_Secret_Is_InvalidSignature()
    {
        var outcome = WebhookHmacSigner.VerifyWithFreshness(
            Secret, Payload, SignedAt, Sign(secret: "other-secret"), nowUnixSeconds: SignedAt, freshnessWindowSeconds: Window);

        outcome.ShouldBe(WebhookVerificationOutcome.InvalidSignature);
    }

    [Fact]
    public void Valid_Signature_Outside_Window_Is_Stale()
    {
        var outcome = WebhookHmacSigner.VerifyWithFreshness(
            Secret, Payload, SignedAt, Sign(), nowUnixSeconds: SignedAt + Window + 1, freshnessWindowSeconds: Window);

        outcome.ShouldBe(WebhookVerificationOutcome.Stale);
    }

    [Fact]
    public void Future_Timestamp_Outside_Window_Is_Stale()
    {
        // Clock skew in the other direction is rejected too (abs difference).
        var outcome = WebhookHmacSigner.VerifyWithFreshness(
            Secret, Payload, SignedAt, Sign(), nowUnixSeconds: SignedAt - (Window + 1), freshnessWindowSeconds: Window);

        outcome.ShouldBe(WebhookVerificationOutcome.Stale);
    }
}
