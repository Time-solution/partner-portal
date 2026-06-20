using System.Collections.Generic;
using Zahy.PartnerPlatform.Partners;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Suggested defaults from DESIGN §3.1 — item-level <see cref="PartnerCatalogOfferingKind"/> still chosen per catalog row.
/// </summary>
public static class PartnerCatalogOfferingKindDefaults
{
    public static IReadOnlyList<PartnerCatalogOfferingKind> GetSuggestedKinds(PartnerType partnerType) =>
        partnerType switch
        {
            PartnerType.Service => new[] { PartnerCatalogOfferingKind.ServiceOneOff, PartnerCatalogOfferingKind.ServiceSubscription },
            PartnerType.Carrier => new[] { PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder },
            PartnerType.ThreePL => new[] { PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder },
            PartnerType.Aggregator => new[]
            {
                PartnerCatalogOfferingKind.FnBItemsPerSale,
                PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder
            },
            PartnerType.Marketplace => new[] { PartnerCatalogOfferingKind.FnBItemsPerSale },
            _ => new[] { PartnerCatalogOfferingKind.ServiceOneOff }
        };

    public static SettlementTriggerMode GetDefaultTriggerMode(PartnerCatalogOfferingKind offeringKind) =>
        offeringKind switch
        {
            PartnerCatalogOfferingKind.ServiceOneOff => SettlementTriggerMode.OnActivation,
            PartnerCatalogOfferingKind.ServiceSubscription => SettlementTriggerMode.PerBillingPeriod,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder => SettlementTriggerMode.PerOrder,
            PartnerCatalogOfferingKind.FnBItemsPerSale => SettlementTriggerMode.PerOrderLine,
            _ => SettlementTriggerMode.OnActivation
        };

    public static SettlementBook GetDefaultSettlementBook(PartnerCatalogOfferingKind offeringKind) =>
        offeringKind switch
        {
            PartnerCatalogOfferingKind.ServiceOneOff => SettlementBook.Integration,
            PartnerCatalogOfferingKind.ServiceSubscription => SettlementBook.Integration,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder => SettlementBook.Marketplace,
            PartnerCatalogOfferingKind.FnBItemsPerSale => SettlementBook.Marketplace,
            _ => SettlementBook.Integration
        };
}
