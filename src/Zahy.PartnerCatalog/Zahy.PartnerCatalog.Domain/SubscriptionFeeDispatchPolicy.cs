namespace Zahy.PartnerCatalog;

/// <summary>
/// SubscriptionFee: Zahy charges merchant a recurring channel fee — output VAT on fee only (2c).
/// </summary>
public static class SubscriptionFeeDispatchPolicy
{
    public static bool ShouldBill(PartnerCatalogItem item, SettlementCostMarkupSnapshot snapshot)
    {
        if (item.SettlementParticipationMode != SettlementParticipationMode.SubscriptionFee)
        {
            return false;
        }

        return item.OfferingKind == PartnerCatalogOfferingKind.ServiceSubscription
               && snapshot.Trigger == SettlementCostMarkupTrigger.BillingPeriod;
    }
}
