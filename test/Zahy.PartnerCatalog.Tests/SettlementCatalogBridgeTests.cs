using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class SettlementCatalogBridgeTests
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111001");
    private static readonly DateTime At = new(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FakeCaseStore : ISettlementCaseStore
    {
        public readonly List<SettlementCase> Cases = new();

        public Task<SettlementCase?> FindByKeyAsync(SettlementBook book, string ext, CancellationToken ct = default) =>
            Task.FromResult(Cases.FirstOrDefault(c => c.Book == book && c.ExternalTransactionId == ext));

        public Task InsertAsync(SettlementCase c, CancellationToken ct = default)
        {
            Cases.Add(c);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SettlementCase c, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (SettlementCatalogBridge Bridge, FakeCaseStore Cases) BuildBridge()
    {
        var cases = new FakeCaseStore();
        var resolver = new SettlementFlowProfileResolver(new ISettlementFlowProfile[]
        {
            new AggregatorFlowProfile(),
            new ServiceFlowProfile()
        });
        var trigger = new SettlementResaleTriggerService(new ResaleVatCalculator(), cases, resolver);
        var bridge = new SettlementCatalogBridge(trigger);
        return (bridge, cases);
    }

    private static (PartnerCatalogItem Item, MerchantActivation Activation, SettlementCostMarkupSnapshot Snapshot)
        CreatePrincipalDeliverySnapshot(decimal buy = 10m, decimal sell = 13m, string ext = "order:ord-7001:v1")
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "DEL-2B",
            "Delivery",
            null,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
            Money.Of(buy, vatInclusive: true));
        item.Publish(At);

        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            item,
            Money.Of(sell, vatInclusive: true));
        activation.Activate(At);

        var snapshot = SettlementCostMarkupSnapshot.Create(
            Guid.NewGuid(),
            activation,
            item,
            Money.Of(sell, vatInclusive: true),
            SettlementCostMarkupTrigger.Order,
            ext);

        return (item, activation, snapshot);
    }

    [Fact]
    public async Task Principal_Delivery_10_To_13_Posts_Balanced_DoubleEntry()
    {
        var (bridge, cases) = BuildBridge();
        var (item, _, snapshot) = CreatePrincipalDeliverySnapshot();

        var result = await bridge.DispatchAsync(snapshot, item);

        result.Outcome.ShouldBe(SettlementCatalogBridgeOutcome.Dispatched);
        result.OutputVat.ShouldBe(1.70m);
        result.InputVat.ShouldBe(1.30m);
        result.NetVatToZatca.ShouldBe(0.40m);
        result.Margin.ShouldBe(2.60m);
        result.TotalDebits.ShouldBe(result.TotalCredits);
        snapshot.SettlementCaseId.ShouldNotBeNull();
        cases.Cases.Count.ShouldBe(1);
        cases.Cases[0].State.ShouldBe(SettlementCaseState.Allocated);
    }

    [Fact]
    public async Task Same_Delivery_Order_Dispatched_Twice_Yields_One_Case()
    {
        var (bridge, cases) = BuildBridge();
        var (item, _, snapshot) = CreatePrincipalDeliverySnapshot();

        await bridge.DispatchAsync(snapshot, item);
        var second = await bridge.DispatchAsync(snapshot, item);

        second.Outcome.ShouldBe(SettlementCatalogBridgeOutcome.Duplicate);
        cases.Cases.Count.ShouldBe(1);
        snapshot.SettlementCaseId.ShouldBe(cases.Cases[0].Id);
    }

    [Fact]
    public async Task Return_On_Delivery_Line_Posts_Balanced_Reversing_Case()
    {
        var (bridge, cases) = BuildBridge();
        var (item, _, snapshot) = CreatePrincipalDeliverySnapshot();

        var dispatch = await bridge.DispatchAsync(snapshot, item);
        var reversal = await bridge.ReverseAsync(snapshot, item);

        reversal.Outcome.ShouldBe(SettlementCatalogBridgeOutcome.Reversed);
        reversal.ReversesSettlementCaseId.ShouldBe(dispatch.SettlementCaseId);
        reversal.TotalDebits.ShouldBe(reversal.TotalCredits);
        reversal.OutputVat.ShouldBe(1.70m);
        reversal.InputVat.ShouldBe(1.30m);
        reversal.Margin.ShouldBe(2.60m);
        cases.Cases.Count.ShouldBe(2);
        cases.Cases[1].ReversesSettlementCaseId.ShouldBe(dispatch.SettlementCaseId);

        (dispatch.TotalDebits + reversal.TotalDebits).ShouldBe(dispatch.TotalCredits + reversal.TotalCredits);
    }

    [Fact]
    public async Task ReflectionOnly_Snapshot_Does_Not_Create_Settlement_Case()
    {
        var (bridge, cases) = BuildBridge();
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "DEL-REF",
            "Delivery reflection",
            null,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
            Money.Of(10m, vatInclusive: true),
            settlementParticipationMode: SettlementParticipationMode.ReflectionOnly);
        item.Publish(At);

        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            item,
            Money.Of(13m, vatInclusive: true));
        activation.Activate(At);

        var snapshot = SettlementCostMarkupSnapshot.Create(
            Guid.NewGuid(),
            activation,
            item,
            Money.Of(13m, vatInclusive: true),
            SettlementCostMarkupTrigger.Order,
            "order:ord-ref:v1");

        var result = await bridge.DispatchAsync(snapshot, item);

        result.Outcome.ShouldBe(SettlementCatalogBridgeOutcome.Skipped);
        snapshot.SettlementCaseId.ShouldBeNull();
        cases.Cases.Count.ShouldBe(0);
    }
}
