using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Zahy.Commission;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class PartnerCatalogParticipationBridgeTests
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

    private sealed class FakeReflectedOrderStore : IReflectedPartnerOrderStore
    {
        public readonly List<ReflectedPartnerOrder> Orders = new();

        public Task<ReflectedPartnerOrder?> FindByKeyAsync(
            string externalTransactionId,
            string orderLineId,
            CancellationToken ct = default) =>
            Task.FromResult(Orders.FirstOrDefault(o =>
                o.ExternalTransactionId == externalTransactionId && o.OrderLineId == orderLineId));

        public Task InsertAsync(ReflectedPartnerOrder order, CancellationToken ct = default)
        {
            Orders.Add(order);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBillingChargeService : IBillingChargeService
    {
        public readonly List<BillingChargeRequest> Requests = new();
        private readonly Dictionary<string, BillingChargeResult> _byKey = new();

        public Task<BillingChargeResult> ChargeAsync(
            BillingChargeRequest request,
            CancellationToken cancellationToken = default)
        {
            if (_byKey.TryGetValue(request.IdempotencyKey, out var existing))
            {
                return Task.FromResult(new BillingChargeResult
                {
                    ChargeId = existing.ChargeId,
                    IsNew = false,
                    Kind = existing.Kind,
                    ChargeTarget = existing.ChargeTarget,
                    Amount = existing.Amount,
                    IdempotencyKey = existing.IdempotencyKey
                });
            }

            var created = new BillingChargeResult
            {
                ChargeId = Guid.NewGuid(),
                IsNew = true,
                Kind = request.Kind,
                ChargeTarget = request.ChargeTarget,
                Amount = request.Amount,
                IdempotencyKey = request.IdempotencyKey
            };
            _byKey[request.IdempotencyKey] = created;
            Requests.Add(request);
            return Task.FromResult(created);
        }

        public Task<BillingChargeResult> ChargeCommissionAccrualAsync(
            CommissionLedgerAccrualResult accrual,
            Guid partnerId,
            Guid? tenantId,
            string currency,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BillingChargeResult> ChargeActivationIfConfiguredAsync(
            Guid partnerId,
            Guid? tenantId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static PartnerCatalogParticipationBridge BuildBridge(
        out FakeCaseStore cases,
        out FakeReflectedOrderStore reflections,
        out FakeBillingChargeService billing)
    {
        cases = new FakeCaseStore();
        reflections = new FakeReflectedOrderStore();
        billing = new FakeBillingChargeService();
        var resolver = new SettlementFlowProfileResolver(new ISettlementFlowProfile[]
        {
            new AggregatorFlowProfile(),
            new ServiceFlowProfile()
        });
        var settlementTrigger = new SettlementResaleTriggerService(new ResaleVatCalculator(), cases, resolver);
        var settlementBridge = new SettlementCatalogBridge(settlementTrigger);
        var reflectionBridge = new ReflectionOnlyOrderBridge(reflections);
        var subscriptionBridge = new SubscriptionFeeBillingBridge(billing, resolver);
        return new PartnerCatalogParticipationBridge(settlementBridge, reflectionBridge, subscriptionBridge);
    }

    [Fact]
    public async Task ReflectionOnly_Order_Creates_Reflection_Record_And_No_Settlement_Case()
    {
        var bridge = BuildBridge(out var cases, out var reflections, out _);
        var (item, activation, snapshot) = CreateFnBReflectionSnapshot();

        var result = await bridge.DispatchAsync(snapshot, item, activation);

        result.Outcome.ShouldBe(PartnerCatalogParticipationOutcome.ReflectionRecorded);
        result.ReflectedPartnerOrderId.ShouldNotBeNull();
        result.SettlementCaseId.ShouldBeNull();
        snapshot.SettlementCaseId.ShouldBeNull();
        reflections.Orders.Count.ShouldBe(1);
        reflections.Orders[0].SettlementCostMarkupSnapshotId.ShouldBe(snapshot.Id);
        cases.Cases.Count.ShouldBe(0);
    }

    [Fact]
    public async Task SubscriptionFee_Monthly_Charge_Posts_Vat_On_Fee_Only_Balanced()
    {
        var bridge = BuildBridge(out _, out _, out var billing);
        var (item, activation, snapshot) = CreateSubscriptionSnapshot(feeInclusive: 115m);

        var result = await bridge.DispatchAsync(snapshot, item, activation, billingPeriodKey: "2026-06");

        result.Outcome.ShouldBe(PartnerCatalogParticipationOutcome.SubscriptionCharged);
        result.OutputVat.ShouldBe(15m);
        result.InputVat.ShouldBe(0m);
        result.TotalDebits.ShouldBe(115m);
        result.TotalCredits.ShouldBe(115m);
        snapshot.BillingChargeId.ShouldNotBeNull();
        billing.Requests.Single().ChargeTarget.ShouldBe(BillingChargeTarget.Merchant);
        billing.Requests.Single().Kind.ShouldBe(BillingChargeKind.Subscription);
    }

    [Fact]
    public async Task Same_Merchant_And_Period_Dispatched_Twice_Yields_One_Charge()
    {
        var bridge = BuildBridge(out _, out _, out var billing);
        var (item, activation, snapshot) = CreateSubscriptionSnapshot(feeInclusive: 115m);

        await bridge.DispatchAsync(snapshot, item, activation, billingPeriodKey: "2026-06");
        var second = await bridge.DispatchAsync(snapshot, item, activation, billingPeriodKey: "2026-06");

        second.Outcome.ShouldBe(PartnerCatalogParticipationOutcome.SubscriptionDuplicate);
        billing.Requests.Count.ShouldBe(1);
        snapshot.BillingChargeId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Principal_Delivery_10_To_13_Regression_Still_Dispatches()
    {
        var bridge = BuildBridge(out var cases, out _, out _);
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "DEL-REG",
            "Delivery",
            null,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
            Money.Of(10m, vatInclusive: true));
        item.Publish(At);
        var activation = MerchantActivation.Create(Guid.NewGuid(), TenantId, item, Money.Of(13m, vatInclusive: true));
        activation.Activate(At);
        var snapshot = SettlementCostMarkupSnapshot.Create(
            Guid.NewGuid(),
            activation,
            item,
            Money.Of(13m, vatInclusive: true),
            SettlementCostMarkupTrigger.Order,
            "order:ord-reg:v1");

        var result = await bridge.DispatchAsync(snapshot, item, activation);

        result.Outcome.ShouldBe(PartnerCatalogParticipationOutcome.PrincipalDispatched);
        result.OutputVat.ShouldBe(1.70m);
        result.InputVat.ShouldBe(1.30m);
        result.TotalDebits.ShouldBe(result.TotalCredits);
        cases.Cases.Count.ShouldBe(1);
    }

    private static (PartnerCatalogItem Item, MerchantActivation Activation, SettlementCostMarkupSnapshot Snapshot)
        CreateFnBReflectionSnapshot()
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "FNB-REF",
            "Burger",
            null,
            PartnerCatalogOfferingKind.FnBItemsPerSale,
            Money.Of(25m, vatInclusive: true),
            externalMenuItemId: "JAHEZ-REF",
            settlementParticipationMode: SettlementParticipationMode.ReflectionOnly);
        item.Publish(At);

        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            item,
            Money.Of(35m, vatInclusive: true));
        activation.Activate(At);

        var snapshot = SettlementCostMarkupSnapshot.Create(
            Guid.NewGuid(),
            activation,
            item,
            Money.Of(40m, vatInclusive: true),
            SettlementCostMarkupTrigger.OrderLine,
            "order:ord-fnb:v1",
            orderLineId: "ORD-FNB-L1");

        snapshot.SellPriceSource.ShouldBe(PartnerCatalogSellPriceSource.Unresolved);
        return (item, activation, snapshot);
    }

    private static (PartnerCatalogItem Item, MerchantActivation Activation, SettlementCostMarkupSnapshot Snapshot)
        CreateSubscriptionSnapshot(decimal feeInclusive)
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "SVC-SUB",
            "Integration channel",
            null,
            PartnerCatalogOfferingKind.ServiceSubscription,
            Money.Of(70m, vatInclusive: true),
            settlementParticipationMode: SettlementParticipationMode.SubscriptionFee);
        item.Publish(At);

        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            item,
            Money.Of(feeInclusive, vatInclusive: true));
        activation.Activate(At);

        var snapshot = SettlementCostMarkupSnapshot.Create(
            Guid.NewGuid(),
            activation,
            item,
            Money.Of(feeInclusive, vatInclusive: true),
            SettlementCostMarkupTrigger.BillingPeriod,
            "pcat:sub:2026-06");

        return (item, activation, snapshot);
    }
}
