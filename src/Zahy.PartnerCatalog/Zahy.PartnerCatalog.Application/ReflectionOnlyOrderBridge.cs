using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Zahy.PartnerCatalog;

public sealed class ReflectionOnlyOrderBridge : ITransientDependency
{
    private readonly IReflectedPartnerOrderStore _orderStore;

    public ReflectionOnlyOrderBridge(IReflectedPartnerOrderStore orderStore)
    {
        _orderStore = orderStore;
    }

    public async Task<(PartnerCatalogParticipationOutcome Outcome, Guid? ReflectedOrderId)> ReflectAsync(
        SettlementCostMarkupSnapshot snapshot,
        PartnerCatalogItem catalogItem,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(snapshot, nameof(snapshot));
        Check.NotNull(catalogItem, nameof(catalogItem));

        if (!ReflectionOnlyDispatchPolicy.ShouldReflect(catalogItem, snapshot))
        {
            return (PartnerCatalogParticipationOutcome.Skipped, null);
        }

        var existing = await _orderStore.FindByKeyAsync(
            snapshot.ExternalTransactionId,
            snapshot.OrderLineId,
            cancellationToken);
        if (existing != null)
        {
            return (PartnerCatalogParticipationOutcome.ReflectionDuplicate, existing.Id);
        }

        var reflected = ReflectedPartnerOrder.Create(Guid.NewGuid(), snapshot, snapshot.CreatedAt);
        await _orderStore.InsertAsync(reflected, cancellationToken);
        return (PartnerCatalogParticipationOutcome.ReflectionRecorded, reflected.Id);
    }
}
