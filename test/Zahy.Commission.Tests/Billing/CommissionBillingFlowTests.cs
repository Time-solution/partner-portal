using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;
using Zahy.OrderLedger;

namespace Zahy.Commission;

public class CommissionBillingFlowTests : ZahyCommissionOrderLedgerTestBase
{
    private readonly IOrderLedgerIngestionService _ingestionService;
    private readonly IRepository<CommissionLedgerEntry, Guid> _ledgerRepository;
    private readonly IRepository<BillingCharge, Guid> _billingRepository;
    private readonly IRepository<PartnerBillingProfile, Guid> _profileRepository;
    private readonly IRepository<CommissionRule, Guid> _ruleRepository;
    private readonly IGuidGenerator _guidGenerator;

    public CommissionBillingFlowTests()
    {
        _ingestionService = GetRequiredService<IOrderLedgerIngestionService>();
        _ledgerRepository = GetRequiredService<IRepository<CommissionLedgerEntry, Guid>>();
        _billingRepository = GetRequiredService<IRepository<BillingCharge, Guid>>();
        _profileRepository = GetRequiredService<IRepository<PartnerBillingProfile, Guid>>();
        _ruleRepository = GetRequiredService<IRepository<CommissionRule, Guid>>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task Paid_Order_Accrual_Creates_Transaction_Billing_Charge()
    {
        var partnerId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedBillingProfileAsync(partnerId, activationFee: 0m);
            await SeedPartnerRuleAsync(partnerId, 0.10m);

            await _ingestionService.IngestSnapshotAsync(CreatePaidSnapshot(
                partnerId,
                "billing-order-1",
                subtotal: 100m,
                taxAmount: 15m,
                deliveryFee: 20m,
                totalAmount: 135m));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var ledger = await _ledgerRepository.GetListAsync();
            ledger.Count.ShouldBe(1);

            var billing = await _billingRepository.GetListAsync();
            billing.Count.ShouldBe(1);
            billing.Single().Kind.ShouldBe(BillingChargeKind.Transaction);
            billing.Single().Amount.ShouldBe(10.00m);
            billing.Single().CommissionLedgerEntryId.ShouldBe(ledger.Single().Id);
        });
    }

    [Fact]
    public async Task Activation_Fee_Charges_Once_Per_Partner()
    {
        var partnerId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedBillingProfileAsync(partnerId, activationFee: 250m);
            await SeedPartnerRuleAsync(partnerId, 0.10m);

            await _ingestionService.IngestSnapshotAsync(CreatePaidSnapshot(
                partnerId, "activation-order-1", 100m, 15m, 20m, 135m));
            await _ingestionService.IngestSnapshotAsync(CreatePaidSnapshot(
                partnerId, "activation-order-2", 50m, 7m, 10m, 67m));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var billing = await _billingRepository.GetListAsync();
            billing.Count(x => x.Kind == BillingChargeKind.ActivationFee).ShouldBe(1);
            billing.Single(x => x.Kind == BillingChargeKind.ActivationFee).Amount.ShouldBe(250m);
            billing.Count(x => x.Kind == BillingChargeKind.Transaction).ShouldBe(2);
        });
    }

    [Fact]
    public async Task Duplicate_Period_Subscription_Does_Not_Double_Charge()
    {
        var partnerId = Guid.NewGuid();
        var billingService = GetRequiredService<IBillingChargeService>();
        var period = "2026-06";
        var idempotencyKey = BillingIdempotency.BuildPeriodKey(partnerId, period);

        await WithUnitOfWorkAsync(async () =>
        {
            var first = await billingService.ChargeAsync(new BillingChargeRequest
            {
                PartnerId = partnerId,
                Kind = BillingChargeKind.Subscription,
                Amount = 99m,
                IdempotencyKey = idempotencyKey,
                PeriodKey = period,
                Description = "June subscription"
            });
            first.IsNew.ShouldBeTrue();

            var second = await billingService.ChargeAsync(new BillingChargeRequest
            {
                PartnerId = partnerId,
                Kind = BillingChargeKind.Subscription,
                Amount = 99m,
                IdempotencyKey = idempotencyKey,
                PeriodKey = period
            });
            second.IsNew.ShouldBeFalse();
            second.ChargeId.ShouldBe(first.ChargeId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            (await _billingRepository.GetListAsync()).Count.ShouldBe(1);
        });
    }

    private async Task SeedBillingProfileAsync(Guid partnerId, decimal activationFee)
    {
        await _profileRepository.InsertAsync(
            new PartnerBillingProfile(
                _guidGenerator.Create(),
                partnerId,
                activationFee,
                monthlySubscriptionAmount: 99m),
            autoSave: true);
    }

    private async Task SeedPartnerRuleAsync(Guid partnerId, decimal percentageRate)
    {
        await _ruleRepository.InsertAsync(new CommissionRule(
            _guidGenerator.Create(),
            "Sale rule",
            CommissionDirection.PlatformEarns,
            CommissionTriggerType.Sale,
            CommissionScopeKind.Partner,
            new CommissionBasisDefinition { PercentageRate = percentageRate },
            DateTime.UtcNow.AddDays(-1),
            CommissionFeeType.Sale,
            scopePartnerId: partnerId), autoSave: true);
    }

    private static OrderSourceSnapshot CreatePaidSnapshot(
        Guid partnerId,
        string sourceOrderId,
        decimal subtotal,
        decimal taxAmount,
        decimal deliveryFee,
        decimal totalAmount) =>
        new()
        {
            SourceSystem = OrderLedgerConsts.FakeSourceSystem,
            SourceOrderId = sourceOrderId,
            SourceVersion = 1,
            TenantId = Guid.NewGuid(),
            PartnerId = partnerId,
            Direction = OrderDirection.Outbound,
            Status = OrderStatus.Paid,
            PaymentStatus = PaymentStatus.Paid,
            Subtotal = subtotal,
            TaxAmount = taxAmount,
            DeliveryFee = deliveryFee,
            TotalAmount = totalAmount,
            SourceTimestamp = DateTime.UtcNow,
            Lines =
            [
                new OrderLineSnapshot
                {
                    LineNumber = 1,
                    Sku = "SKU-1",
                    ProductName = "Item",
                    Quantity = 1,
                    UnitPrice = subtotal,
                    LineTotal = subtotal
                }
            ]
        };
}
