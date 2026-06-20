namespace Zahy.Settlement;

public interface ISettlementWebhookSignatureVerifier
{
    WebhookSignatureStatus Verify(InboundSettlementWebhook webhook);
}

/// <summary>
/// Verifies the inbound webhook's HMAC using the EXISTING Zahy.Webhooks primitive (WebhookHmacSigner).
/// A missing signature is treated as hostile (never processed).
/// </summary>
public sealed class HmacSettlementWebhookSignatureVerifier : ISettlementWebhookSignatureVerifier
{
    public WebhookSignatureStatus Verify(InboundSettlementWebhook webhook)
    {
        if (string.IsNullOrWhiteSpace(webhook.Signature))
        {
            return WebhookSignatureStatus.Missing;
        }

        var ok = Zahy.Webhooks.WebhookHmacSigner.Verify(
            webhook.SigningSecret,
            webhook.Payload,
            webhook.UnixTimestamp,
            webhook.Signature!);

        return ok ? WebhookSignatureStatus.Valid : WebhookSignatureStatus.Invalid;
    }
}
