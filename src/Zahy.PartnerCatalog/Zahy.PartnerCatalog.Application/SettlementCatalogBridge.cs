using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Partner Catalog → Settlement anti-corruption bridge. Computation + Allocated case only;
/// disbursement and live ZATCA remain flagged OFF in the settlement engine.
/// </summary>
public sealed class SettlementCatalogBridge : ISettlementCatalogBridge, ITransientDependency
{
    private const decimal DefaultVatRate = SettlementVatOptions.DefaultStandardRate;

    private readonly ISettlementResaleTriggerPort _settlementTrigger;

    public SettlementCatalogBridge(ISettlementResaleTriggerPort settlementTrigger)
    {
        _settlementTrigger = settlementTrigger;
    }

    public async Task<SettlementCatalogBridgeResult> DispatchAsync(
        SettlementCostMarkupSnapshot snapshot,
        PartnerCatalogItem catalogItem,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(snapshot, nameof(snapshot));
        Check.NotNull(catalogItem, nameof(catalogItem));

        if (!PrincipalSettlementDispatchPolicy.ShouldDispatch(catalogItem, snapshot))
        {
            return new SettlementCatalogBridgeResult
            {
                Outcome = SettlementCatalogBridgeOutcome.Skipped,
                SkipReason = catalogItem.SettlementParticipationMode != SettlementParticipationMode.Principal
                    ? $"ParticipationMode={catalogItem.SettlementParticipationMode}"
                    : $"OfferingKind={catalogItem.OfferingKind},Trigger={snapshot.Trigger}"
            };
        }

        var triggerResult = await _settlementTrigger.TriggerPrincipalResaleAsync(
            new SettlementResaleTriggerRequest
            {
                Book = snapshot.SettlementBook,
                PartnerId = snapshot.PartnerId,
                ExternalTransactionId = snapshot.ExternalTransactionId,
                BuyPrice = snapshot.BuyPrice,
                SellPrice = snapshot.SellPrice,
                VatRate = DefaultVatRate,
                PostedAt = snapshot.CreatedAt,
                Reference = $"pcat:snapshot:{snapshot.Id:D}"
            },
            cancellationToken);

        if (!triggerResult.IsDuplicate)
        {
            snapshot.LinkSettlementCase(triggerResult.SettlementCaseId);
        }
        else if (!snapshot.SettlementCaseId.HasValue)
        {
            snapshot.LinkSettlementCase(triggerResult.SettlementCaseId);
        }

        return MapOutcome(
            triggerResult,
            triggerResult.IsDuplicate
                ? SettlementCatalogBridgeOutcome.Duplicate
                : SettlementCatalogBridgeOutcome.Dispatched);
    }

    public async Task<SettlementCatalogBridgeResult> ReverseAsync(
        SettlementCostMarkupSnapshot originalSnapshot,
        PartnerCatalogItem catalogItem,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(originalSnapshot, nameof(originalSnapshot));
        Check.NotNull(catalogItem, nameof(catalogItem));

        if (!originalSnapshot.SettlementCaseId.HasValue)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidSettlementLink)
                .WithData("Reason", "OriginalSnapshotNotLinked");
        }

        if (!PrincipalSettlementDispatchPolicy.ShouldDispatch(catalogItem, originalSnapshot))
        {
            return new SettlementCatalogBridgeResult
            {
                Outcome = SettlementCatalogBridgeOutcome.Skipped,
                SkipReason = "ReverseNotApplicableForParticipationOrKind"
            };
        }

        var reversalExternalId = PartnerCatalogSnapshotIdempotency.BuildReversalExternalTransactionId(
            originalSnapshot.ExternalTransactionId,
            originalSnapshot.OrderLineId);

        var triggerResult = await _settlementTrigger.ReversePrincipalResaleAsync(
            new SettlementResaleReversalRequest
            {
                Book = originalSnapshot.SettlementBook,
                PartnerId = originalSnapshot.PartnerId,
                OriginalSettlementCaseId = originalSnapshot.SettlementCaseId.Value,
                ReversalExternalTransactionId = reversalExternalId,
                BuyPrice = originalSnapshot.BuyPrice,
                SellPrice = originalSnapshot.SellPrice,
                VatRate = DefaultVatRate,
                PostedAt = originalSnapshot.CreatedAt,
                Reference = $"pcat:snapshot:{originalSnapshot.Id:D}:rev"
            },
            cancellationToken);

        return MapOutcome(
            triggerResult,
            triggerResult.IsDuplicate
                ? SettlementCatalogBridgeOutcome.Duplicate
                : SettlementCatalogBridgeOutcome.Reversed);
    }

    private static SettlementCatalogBridgeResult MapOutcome(
        SettlementResaleTriggerResult triggerResult,
        SettlementCatalogBridgeOutcome outcome) =>
        new()
        {
            Outcome = outcome,
            SettlementCaseId = triggerResult.SettlementCaseId,
            ReversesSettlementCaseId = triggerResult.ReversesSettlementCaseId,
            OutputVat = triggerResult.OutputVat,
            InputVat = triggerResult.InputVat,
            Margin = triggerResult.Margin,
            NetVatToZatca = triggerResult.NetVatToZatca,
            TotalDebits = triggerResult.TotalDebits,
            TotalCredits = triggerResult.TotalCredits
        };
}
