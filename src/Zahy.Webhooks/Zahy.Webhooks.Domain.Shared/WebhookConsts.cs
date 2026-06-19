namespace Zahy.Webhooks;

public static class WebhookConsts
{
    public const int MaxTargetUrlLength = 2048;
    public const int MaxEventTypeLength = 128;
    public const int MaxIdempotencyKeyLength = 256;
    public const int MaxPayloadLength = 65536;
    public const int MaxResponseSnippetLength = 4096;
    public const int MaxReasonLength = 512;
    public const int MaxSigningSecretLength = 128;
    public const int DefaultMaxDeliveryAttempts = 5;
}
