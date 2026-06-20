using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Zahy.PartnerCatalog;

/// <summary>
/// On a sale from the partner warehouse, route to the ownership mode's EXISTING settlement mode
/// (MerchantOwned → ReflectionOnly, PartnerBought → Principal). Reuses the existing participation
/// bridge — no new settlement logic. Dispatch is FLAGGED OFF by default (no money moved); the final
/// ownership mode is confirmed with the accountant at go-live, like other flows.
/// </summary>
public sealed class ConsignmentSaleSettlementRouter : IConsignmentSaleSettlementRouter, ITransientDependency
{
    private readonly IPartnerCatalogParticipationBridge _participationBridge;
    private readonly ConsignmentSettlementOptions _options;

    public ConsignmentSaleSettlementRouter(
        IPartnerCatalogParticipationBridge participationBridge,
        IOptions<ConsignmentSettlementOptions> options)
    {
        _participationBridge = participationBridge;
        _options = options.Value;
    }

    public async Task<ConsignmentSaleRoutingResult> RouteSaleAsync(
        SettlementCostMarkupSnapshot snapshot,
        PartnerCatalogItem catalogItem,
        MerchantActivation activation,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(snapshot, nameof(snapshot));
        Check.NotNull(catalogItem, nameof(catalogItem));
        Check.NotNull(activation, nameof(activation));

        if (catalogItem.OfferingKind != PartnerCatalogOfferingKind.ConsignmentFulfilment)
        {
            return new ConsignmentSaleRoutingResult
            {
                Outcome = ConsignmentSaleRoutingOutcome.Skipped,
                SkipReason = $"OfferingKind={catalogItem.OfferingKind}"
            };
        }

        var ownershipMode = catalogItem.ConsignmentOwnershipMode ?? ConsignmentOwnershipModeRouting.DefaultMode;
        var resolvedMode = ConsignmentOwnershipModeRouting.ResolveParticipationMode(ownershipMode);

        // FLAGGED OFF: resolve and record the routing decision only — never touch money.
        if (!_options.SettlementDispatchEnabled)
        {
            return new ConsignmentSaleRoutingResult
            {
                Outcome = ConsignmentSaleRoutingOutcome.FlaggedOff,
                ResolvedOwnershipMode = ownershipMode,
                ResolvedParticipationMode = resolvedMode,
                SkipReason = "ConsignmentSettlementDispatchFlaggedOff"
            };
        }

        // Go-live path (not exercised while flagged off): reuse the existing participation bridge,
        // which itself keeps disbursement + live ZATCA gated in the settlement engine.
        var participationResult = await _participationBridge.DispatchAsync(
            snapshot,
            catalogItem,
            activation,
            cancellationToken: cancellationToken);

        return new ConsignmentSaleRoutingResult
        {
            Outcome = ConsignmentSaleRoutingOutcome.Dispatched,
            ResolvedOwnershipMode = ownershipMode,
            ResolvedParticipationMode = resolvedMode,
            ParticipationResult = participationResult
        };
    }
}
