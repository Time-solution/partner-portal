using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Zahy.PartnerCatalog;

public sealed class PartnerCatalogParticipationBridge : IPartnerCatalogParticipationBridge, ITransientDependency
{
    private readonly ISettlementCatalogBridge _settlementBridge;
    private readonly ReflectionOnlyOrderBridge _reflectionBridge;
    private readonly SubscriptionFeeBillingBridge _subscriptionBridge;

    public PartnerCatalogParticipationBridge(
        ISettlementCatalogBridge settlementBridge,
        ReflectionOnlyOrderBridge reflectionBridge,
        SubscriptionFeeBillingBridge subscriptionBridge)
    {
        _settlementBridge = settlementBridge;
        _reflectionBridge = reflectionBridge;
        _subscriptionBridge = subscriptionBridge;
    }

    public async Task<PartnerCatalogParticipationResult> DispatchAsync(
        SettlementCostMarkupSnapshot snapshot,
        PartnerCatalogItem catalogItem,
        MerchantActivation activation,
        string? billingPeriodKey = null,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(snapshot, nameof(snapshot));
        Check.NotNull(catalogItem, nameof(catalogItem));
        Check.NotNull(activation, nameof(activation));

        return catalogItem.SettlementParticipationMode switch
        {
            SettlementParticipationMode.Principal => await DispatchPrincipalAsync(snapshot, catalogItem, cancellationToken),
            SettlementParticipationMode.ReflectionOnly => await DispatchReflectionAsync(snapshot, catalogItem, cancellationToken),
            SettlementParticipationMode.SubscriptionFee => await DispatchSubscriptionAsync(
                snapshot,
                catalogItem,
                activation,
                billingPeriodKey,
                cancellationToken),
            _ => Skipped($"ParticipationMode={catalogItem.SettlementParticipationMode}")
        };
    }

    public async Task<PartnerCatalogParticipationResult> ReversePrincipalAsync(
        SettlementCostMarkupSnapshot originalSnapshot,
        PartnerCatalogItem catalogItem,
        CancellationToken cancellationToken = default)
    {
        var result = await _settlementBridge.ReverseAsync(originalSnapshot, catalogItem, cancellationToken);
        return new PartnerCatalogParticipationResult
        {
            Outcome = result.Outcome switch
            {
                SettlementCatalogBridgeOutcome.Reversed => PartnerCatalogParticipationOutcome.PrincipalReversed,
                SettlementCatalogBridgeOutcome.Duplicate => PartnerCatalogParticipationOutcome.PrincipalDuplicate,
                SettlementCatalogBridgeOutcome.Skipped => PartnerCatalogParticipationOutcome.Skipped,
                _ => PartnerCatalogParticipationOutcome.Skipped
            },
            SettlementCaseId = result.SettlementCaseId,
            SkipReason = result.SkipReason,
            OutputVat = result.OutputVat,
            InputVat = result.InputVat,
            Margin = result.Margin,
            NetVatToZatca = result.NetVatToZatca,
            TotalDebits = result.TotalDebits,
            TotalCredits = result.TotalCredits
        };
    }

    private async Task<PartnerCatalogParticipationResult> DispatchPrincipalAsync(
        SettlementCostMarkupSnapshot snapshot,
        PartnerCatalogItem catalogItem,
        CancellationToken cancellationToken)
    {
        var result = await _settlementBridge.DispatchAsync(snapshot, catalogItem, cancellationToken);
        return new PartnerCatalogParticipationResult
        {
            Outcome = result.Outcome switch
            {
                SettlementCatalogBridgeOutcome.Dispatched => PartnerCatalogParticipationOutcome.PrincipalDispatched,
                SettlementCatalogBridgeOutcome.Duplicate => PartnerCatalogParticipationOutcome.PrincipalDuplicate,
                _ => PartnerCatalogParticipationOutcome.Skipped
            },
            SettlementCaseId = result.SettlementCaseId,
            SkipReason = result.SkipReason,
            OutputVat = result.OutputVat,
            InputVat = result.InputVat,
            Margin = result.Margin,
            NetVatToZatca = result.NetVatToZatca,
            TotalDebits = result.TotalDebits,
            TotalCredits = result.TotalCredits
        };
    }

    private async Task<PartnerCatalogParticipationResult> DispatchReflectionAsync(
        SettlementCostMarkupSnapshot snapshot,
        PartnerCatalogItem catalogItem,
        CancellationToken cancellationToken)
    {
        var (outcome, reflectedOrderId) = await _reflectionBridge.ReflectAsync(snapshot, catalogItem, cancellationToken);
        if (outcome == PartnerCatalogParticipationOutcome.Skipped)
        {
            return Skipped("ReflectionNotApplicableForKindOrTrigger");
        }

        return new PartnerCatalogParticipationResult
        {
            Outcome = outcome,
            ReflectedPartnerOrderId = reflectedOrderId
        };
    }

    private async Task<PartnerCatalogParticipationResult> DispatchSubscriptionAsync(
        SettlementCostMarkupSnapshot snapshot,
        PartnerCatalogItem catalogItem,
        MerchantActivation activation,
        string? billingPeriodKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(billingPeriodKey))
        {
            return Skipped("BillingPeriodKeyRequired");
        }

        var (outcome, billingChargeId, outputVat, totalDebits, totalCredits) =
            await _subscriptionBridge.ChargeAsync(
                snapshot,
                catalogItem,
                activation,
                billingPeriodKey,
                cancellationToken);

        if (outcome == PartnerCatalogParticipationOutcome.Skipped)
        {
            return Skipped("SubscriptionNotApplicableForKindOrTrigger");
        }

        return new PartnerCatalogParticipationResult
        {
            Outcome = outcome,
            BillingChargeId = billingChargeId,
            OutputVat = outputVat,
            InputVat = 0m,
            TotalDebits = totalDebits,
            TotalCredits = totalCredits
        };
    }

    private static PartnerCatalogParticipationResult Skipped(string reason) =>
        new()
        {
            Outcome = PartnerCatalogParticipationOutcome.Skipped,
            SkipReason = reason
        };
}
