namespace Zahy.PartnerCatalog;

/// <summary>
/// JUMP / Consignment-Fulfilment feature flags. Settlement dispatch on a warehouse sale stays OFF
/// until accountant + CTO go-live wiring — no real charge, no disbursement, no live ZATCA.
/// </summary>
public class ConsignmentSettlementOptions
{
    public const string SectionName = "Zahy:PartnerCatalog:Consignment";

    /// <summary>
    /// When false (default), a sale from the partner warehouse only RESOLVES the settlement mode
    /// (ReflectionOnly / Principal) and records the routing decision — no money is moved.
    /// </summary>
    public bool SettlementDispatchEnabled { get; set; }
}
