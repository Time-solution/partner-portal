namespace Zahy.PartnerCatalog;

/// <summary>
/// Routes Partner Catalog snapshots into settlement (2b). Only Principal participation dispatches here.
/// </summary>
public static class PrincipalSettlementDispatchPolicy
{
    public static bool ShouldDispatch(PartnerCatalogItem item, SettlementCostMarkupSnapshot snapshot)
    {
        if (item.SettlementParticipationMode != SettlementParticipationMode.Principal)
        {
            return false;
        }

        return item.OfferingKind switch
        {
            PartnerCatalogOfferingKind.ServiceOneOff =>
                snapshot.Trigger is SettlementCostMarkupTrigger.Activation,
            PartnerCatalogOfferingKind.ServiceSubscription =>
                snapshot.Trigger is SettlementCostMarkupTrigger.Activation
                    or SettlementCostMarkupTrigger.BillingPeriod,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder =>
                snapshot.Trigger is SettlementCostMarkupTrigger.Order
                && snapshot.OrderLineId == string.Empty,
            _ => false
        };
    }
}
