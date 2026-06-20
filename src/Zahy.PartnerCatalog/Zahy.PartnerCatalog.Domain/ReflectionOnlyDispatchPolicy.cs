namespace Zahy.PartnerCatalog;

/// <summary>
/// ReflectionOnly: aggregator collects from customer and pays merchant — Zahy reflects order only (2c).
/// </summary>
public static class ReflectionOnlyDispatchPolicy
{
    public static bool ShouldReflect(PartnerCatalogItem item, SettlementCostMarkupSnapshot snapshot)
    {
        if (item.SettlementParticipationMode != SettlementParticipationMode.ReflectionOnly)
        {
            return false;
        }

        return item.OfferingKind switch
        {
            PartnerCatalogOfferingKind.FnBItemsPerSale =>
                snapshot.Trigger is SettlementCostMarkupTrigger.OrderLine
                    or SettlementCostMarkupTrigger.Order,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder =>
                snapshot.Trigger is SettlementCostMarkupTrigger.Order
                && snapshot.OrderLineId == string.Empty,
            // Consignment sale from the partner warehouse — MerchantOwned custody reflects per sold line.
            PartnerCatalogOfferingKind.ConsignmentFulfilment =>
                snapshot.Trigger is SettlementCostMarkupTrigger.OrderLine
                    or SettlementCostMarkupTrigger.Order,
            _ => false
        };
    }
}
