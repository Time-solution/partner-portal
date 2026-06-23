using System;
using Microsoft.Extensions.Options;
using Volo.Abp.Timing;
using Zahy.Webhooks;

namespace Zahy.Settlement;

public interface ISettlementWebhookSignatureVerifier
{
    WebhookSignatureStatus Verify(InboundSettlementWebhook webhook);
}

/// <summary>
/// Verifies the inbound webhook's HMAC using the EXISTING Zahy.Webhooks primitive (WebhookHmacSigner).
/// The signing secret is resolved server-side per (Book, PartnerId) — never trusted from the caller.
/// A missing signature is treated as hostile; a stale timestamp (outside the replay window) is rejected
/// even when the signature itself is valid.
/// </summary>
public sealed class HmacSettlementWebhookSignatureVerifier : ISettlementWebhookSignatureVerifier
{
    private readonly ISigningSecretResolver _secretResolver;
    private readonly IClock _clock;
    private readonly SettlementWebhookSecurityOptions _options;

    public HmacSettlementWebhookSignatureVerifier(
        ISigningSecretResolver secretResolver,
        IClock clock,
        IOptions<SettlementWebhookSecurityOptions> options)
    {
        _secretResolver = secretResolver;
        _clock = clock;
        _options = options.Value;
    }

    public WebhookSignatureStatus Verify(InboundSettlementWebhook webhook)
    {
        if (string.IsNullOrWhiteSpace(webhook.Signature))
        {
            return WebhookSignatureStatus.Missing;
        }

        var secret = _secretResolver.Resolve(webhook.Book, webhook.PartnerId);
        if (string.IsNullOrEmpty(secret))
        {
            // No server-side secret on file ⇒ we cannot prove authenticity ⇒ treat as hostile.
            return WebhookSignatureStatus.Invalid;
        }

        var nowUnixSeconds = new DateTimeOffset(_clock.Now.ToUniversalTime()).ToUnixTimeSeconds();

        var outcome = WebhookHmacSigner.VerifyWithFreshness(
            secret,
            webhook.Payload,
            webhook.UnixTimestamp,
            webhook.Signature!,
            nowUnixSeconds,
            _options.FreshnessWindowSeconds);

        return outcome switch
        {
            WebhookVerificationOutcome.Valid => WebhookSignatureStatus.Valid,
            WebhookVerificationOutcome.Stale => WebhookSignatureStatus.Stale,
            _ => WebhookSignatureStatus.Invalid
        };
    }
}
