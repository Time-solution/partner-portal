namespace Zahy.PartnerCatalog;

/// <summary>
/// Merchant self-service write API feature flags. Billing/participation bridges stay OFF until CTO wiring.
/// </summary>
public class PartnerCatalogMerchantOptions
{
    public const string SectionName = "Zahy:PartnerCatalog:Merchant";

    /// <summary>When false (default), activation records only — no subscription billing charges.</summary>
    public bool SubscriptionBillingEnabled { get; set; }

    /// <summary>When false (default), no settlement/reflection dispatch on activation.</summary>
    public bool ParticipationBridgeEnabled { get; set; }
}
