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
            // ThreePL can hold stock in custody (JUMP "Fulfilled by") in addition to plain fulfilment.
            PartnerType.ThreePL => new[]
            {
                PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
                PartnerCatalogOfferingKind.ConsignmentFulfilment
            },
            PartnerType.Aggregator => new[]
            {
                PartnerCatalogOfferingKind.FnBItemsPerSale,
                PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder
            },
            // Marketplace (noon JUMP-shaped) can resell via consignment custody as well as F&B items.
            PartnerType.Marketplace => new[]
            {
                PartnerCatalogOfferingKind.FnBItemsPerSale,
                PartnerCatalogOfferingKind.ConsignmentFulfilment
            },
            _ => new[] { PartnerCatalogOfferingKind.ServiceOneOff }
        };

    public static SettlementTriggerMode GetDefaultTriggerMode(PartnerCatalogOfferingKind offeringKind) =>
        offeringKind switch
        {
            PartnerCatalogOfferingKind.ServiceOneOff => SettlementTriggerMode.OnActivation,
            PartnerCatalogOfferingKind.ServiceSubscription => SettlementTriggerMode.PerBillingPeriod,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder => SettlementTriggerMode.PerOrder,
            PartnerCatalogOfferingKind.FnBItemsPerSale => SettlementTriggerMode.PerOrderLine,
            // Consignment fulfils per sold unit from the partner warehouse — settle per order line.
            PartnerCatalogOfferingKind.ConsignmentFulfilment => SettlementTriggerMode.PerOrderLine,
            _ => SettlementTriggerMode.OnActivation
        };

    public static SettlementBook GetDefaultSettlementBook(PartnerCatalogOfferingKind offeringKind) =>
        offeringKind switch
        {
            PartnerCatalogOfferingKind.ServiceOneOff => SettlementBook.Integration,
            PartnerCatalogOfferingKind.ServiceSubscription => SettlementBook.Integration,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder => SettlementBook.Marketplace,
            PartnerCatalogOfferingKind.FnBItemsPerSale => SettlementBook.Marketplace,
            PartnerCatalogOfferingKind.ConsignmentFulfilment => SettlementBook.Marketplace,
            _ => SettlementBook.Integration
        };
}
