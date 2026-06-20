using System.Threading;
using System.Threading.Tasks;

namespace Zahy.PartnerCatalog;

public enum ConsignmentSaleRoutingOutcome
{
    /// <summary>Mode resolved but dispatch is flagged off — no money moved (default behaviour).</summary>
    FlaggedOff = 1,

    /// <summary>Flag on — dispatched to the existing settlement participation bridge.</summary>
    Dispatched = 2,

    /// <summary>Not a consignment offering / not eligible for sale-driven settlement.</summary>
    Skipped = 3
}

public sealed class ConsignmentSaleRoutingResult
{
    public ConsignmentSaleRoutingOutcome Outcome { get; init; }

    /// <summary>The EXISTING settlement mode the ownership mode routes to (ReflectionOnly / Principal).</summary>
    public SettlementParticipationMode ResolvedParticipationMode { get; init; }

    public ConsignmentOwnershipMode ResolvedOwnershipMode { get; init; }

    /// <summary>Populated only when the flag is on and the bridge actually ran.</summary>
    public PartnerCatalogParticipationResult? ParticipationResult { get; init; }

    public string? SkipReason { get; init; }
}

/// <summary>
/// Routes a sale from the partner warehouse (consignment custody) to the correct EXISTING settlement
/// mode based on <see cref="ConsignmentOwnershipMode"/>. Dispatch is FLAGGED OFF by default — it only
/// resolves and records the routing decision; no charge, no disbursement, no live ZATCA.
/// </summary>
public interface IConsignmentSaleSettlementRouter
{
    Task<ConsignmentSaleRoutingResult> RouteSaleAsync(
        SettlementCostMarkupSnapshot snapshot,
        PartnerCatalogItem catalogItem,
        MerchantActivation activation,
        CancellationToken cancellationToken = default);
}
