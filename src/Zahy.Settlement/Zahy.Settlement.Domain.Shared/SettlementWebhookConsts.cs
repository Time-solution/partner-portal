namespace Zahy.Settlement;

public static class SettlementWebhookConsts
{
    public const int MaxExternalEventIdLength = 256;
    public const int MaxRawPayloadLength = 16384;
    public const int MaxReasonLength = 1024;
}

/// <summary>Phase-4 (webhook ingestion + allocation) error codes.</summary>
public static class SettlementWebhookErrorCodes
{
    public const string Namespace = "Zahy.Settlement";

    public const string AllocationDoesNotBalance = Namespace + ":044";
    public const string AllocationNotConfiguredForBook = Namespace + ":045";
}
