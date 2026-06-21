using System;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Activation-time snapshot shape — maps catalog trigger mode to snapshot trigger + idempotency keys.
/// </summary>
public static class PartnerCatalogActivationSnapshotDefaults
{
    public static (SettlementCostMarkupTrigger Trigger, string? OrderLineId) ResolveSnapshotShape(
        PartnerCatalogItem item)
    {
        var trigger = item.SettlementTriggerMode switch
        {
            SettlementTriggerMode.OnActivation => SettlementCostMarkupTrigger.Activation,
            SettlementTriggerMode.PerBillingPeriod => SettlementCostMarkupTrigger.BillingPeriod,
            SettlementTriggerMode.PerOrder => SettlementCostMarkupTrigger.Order,
            // Per-order-line kinds use an order-scoped activation handshake snapshot (empty line id).
            SettlementTriggerMode.PerOrderLine => SettlementCostMarkupTrigger.Order,
            _ => SettlementCostMarkupTrigger.Activation
        };

        return (trigger, null);
    }

    /// <summary>Stable external id tied to activation idempotency — one snapshot per activation.</summary>
    public static string BuildExternalTransactionId(MerchantActivation activation) =>
        PartnerCatalogSnapshotIdempotency.NormalizeExternalTransactionId(
            $"pcat:activation:{activation.IdempotencyKey}");

    public static string ResolveBillingPeriodKey(DateTime atUtc) =>
        atUtc.ToString("yyyy-MM");
}
