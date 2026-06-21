using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace Zahy.PartnerCatalog.Merchant;

/// <summary>
/// Phase 3 — gated snapshot-on-activate orchestration. Reuses domain snapshot factory + participation bridge.
/// </summary>
public class PartnerCatalogActivationSnapshotOrchestrator : DomainService
{
    private readonly IRepository<SettlementCostMarkupSnapshot, Guid> _snapshotRepository;
    private readonly IPartnerCatalogParticipationBridge _participationBridge;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IClock _clock;

    public PartnerCatalogActivationSnapshotOrchestrator(
        IRepository<SettlementCostMarkupSnapshot, Guid> snapshotRepository,
        IPartnerCatalogParticipationBridge participationBridge,
        IGuidGenerator guidGenerator,
        IClock clock)
    {
        _snapshotRepository = snapshotRepository;
        _participationBridge = participationBridge;
        _guidGenerator = guidGenerator;
        _clock = clock;
    }

    public async Task<PartnerCatalogParticipationResult?> TryDispatchOnActivateAsync(
        MerchantActivation activation,
        PartnerCatalogItem catalogItem,
        PartnerCatalogMerchantOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!options.ParticipationBridgeEnabled)
        {
            return null;
        }

        var atUtc = _clock.Now.ToUniversalTime();
        var snapshot = await FindOrCreateSnapshotAsync(activation, catalogItem, atUtc, cancellationToken);

        string? billingPeriodKey = null;
        if (options.SubscriptionBillingEnabled &&
            catalogItem.SettlementParticipationMode == SettlementParticipationMode.SubscriptionFee)
        {
            billingPeriodKey = PartnerCatalogActivationSnapshotDefaults.ResolveBillingPeriodKey(atUtc);
        }

        var result = await _participationBridge.DispatchAsync(
            snapshot,
            catalogItem,
            activation,
            billingPeriodKey,
            cancellationToken);

        await _snapshotRepository.UpdateAsync(snapshot, autoSave: true, cancellationToken: cancellationToken);

        return result;
    }

    private async Task<SettlementCostMarkupSnapshot> FindOrCreateSnapshotAsync(
        MerchantActivation activation,
        PartnerCatalogItem catalogItem,
        DateTime atUtc,
        CancellationToken cancellationToken)
    {
        var existing = await _snapshotRepository.FirstOrDefaultAsync(
            x => x.MerchantActivationId == activation.Id,
            cancellationToken: cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var (trigger, orderLineId) = PartnerCatalogActivationSnapshotDefaults.ResolveSnapshotShape(catalogItem);
        var externalId = PartnerCatalogActivationSnapshotDefaults.BuildExternalTransactionId(activation);

        var snapshot = SettlementCostMarkupSnapshot.Create(
            _guidGenerator.Create(),
            activation,
            catalogItem,
            activation.ResalePrice,
            trigger,
            externalId,
            orderLineId,
            createdAt: atUtc);

        await _snapshotRepository.InsertAsync(snapshot, autoSave: true, cancellationToken: cancellationToken);

        return snapshot;
    }
}
