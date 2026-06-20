using Volo.Abp;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Idempotency dimensions for <c>SettlementCostMarkupSnapshot</c> (P3 entity).
/// Mirrors Settlement's unique (Book, ExternalTransactionId) discipline: per-order and per-order-line
/// triggers combine <see cref="ExternalTransactionId"/> with <see cref="OrderLineId"/> so two lines
/// on the same parent order cannot collide when the external id is order-scoped.
/// </summary>
public static class PartnerCatalogSnapshotIdempotency
{
    public static string NormalizeExternalTransactionId(string externalTransactionId)
    {
        if (string.IsNullOrWhiteSpace(externalTransactionId))
        {
            throw new BusinessException(PartnerCatalogErrorCodes.EmptyExternalTransactionId);
        }

        return externalTransactionId.Trim().ToLowerInvariant();
    }

    /// <summary>Null/blank order line id maps to empty string for non-line triggers (activation, whole-order delivery).</summary>
    public static string NormalizeOrderLineId(string? orderLineId) =>
        string.IsNullOrWhiteSpace(orderLineId) ? string.Empty : orderLineId.Trim().ToLowerInvariant();

    public static (string ExternalTransactionId, string OrderLineId) BuildUniqueKeyComponents(
        string externalTransactionId,
        string? orderLineId,
        SettlementCostMarkupTrigger trigger)
    {
        var normalizedExternal = NormalizeExternalTransactionId(externalTransactionId);
        var normalizedLine = NormalizeOrderLineId(orderLineId);

        if (trigger == SettlementCostMarkupTrigger.OrderLine && string.IsNullOrEmpty(normalizedLine))
        {
            throw new BusinessException(PartnerCatalogErrorCodes.MissingOrderLineId)
                .WithData("Trigger", trigger.ToString());
        }

        return (normalizedExternal, normalizedLine);
    }

    /// <summary>Composite storage key persisted for explain/debug; EF unique index is (ExternalTransactionId, OrderLineId).</summary>
    public static string BuildStorageKey(string externalTransactionId, string? orderLineId) =>
        NormalizeExternalTransactionId(externalTransactionId) + "|" + NormalizeOrderLineId(orderLineId);

    /// <summary>Per-line reversal idempotency key — distinct from the original dispatch external id.</summary>
    public static string BuildReversalExternalTransactionId(string originalExternalTransactionId, string? orderLineId) =>
        NormalizeExternalTransactionId(originalExternalTransactionId) + ":rev:" + NormalizeOrderLineId(orderLineId);
}
