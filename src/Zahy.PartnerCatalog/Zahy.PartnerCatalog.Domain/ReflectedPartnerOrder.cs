using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Read-only order projection for ReflectionOnly participation (HungerStation F&amp;B, marketplace).
/// Zahy is NOT in the money flow — no sale, no VAT, no settlement case.
/// </summary>
public class ReflectedPartnerOrder : Entity<Guid>
{
    public Guid PartnerId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid MerchantActivationId { get; private set; }

    public Guid PartnerCatalogItemId { get; private set; }

    public Guid SettlementCostMarkupSnapshotId { get; private set; }

    public string ExternalTransactionId { get; private set; } = string.Empty;

    /// <summary>Normalized storage key; empty when whole-order reflection.</summary>
    public string OrderLineId { get; private set; } = string.Empty;

    public DateTime ReflectedAt { get; private set; }

    protected ReflectedPartnerOrder()
    {
    }

    public static ReflectedPartnerOrder Create(
        Guid id,
        SettlementCostMarkupSnapshot snapshot,
        DateTime reflectedAt)
    {
        Check.NotNull(snapshot, nameof(snapshot));

        return new ReflectedPartnerOrder
        {
            Id = id,
            PartnerId = snapshot.PartnerId,
            TenantId = snapshot.TenantId,
            MerchantActivationId = snapshot.MerchantActivationId,
            PartnerCatalogItemId = snapshot.PartnerCatalogItemId,
            SettlementCostMarkupSnapshotId = snapshot.Id,
            ExternalTransactionId = snapshot.ExternalTransactionId,
            OrderLineId = snapshot.OrderLineId,
            ReflectedAt = reflectedAt
        };
    }
}
