using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;
using Zahy.OrderLedger;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.Commission;

public class CommissionOrderAccrualFlowTests : ZahyCommissionOrderLedgerTestBase
{
    private readonly IOrderLedgerIngestionService _ingestionService;
    private readonly IRepository<CommissionLedgerEntry, Guid> _ledgerRepository;
    private readonly IRepository<CommissionRule, Guid> _ruleRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly TestCommissionPartnerTypeLookup _partnerTypeLookup;

    public CommissionOrderAccrualFlowTests()
    {
        _ingestionService = GetRequiredService<IOrderLedgerIngestionService>();
        _ledgerRepository = GetRequiredService<IRepository<CommissionLedgerEntry, Guid>>();
        _ruleRepository = GetRequiredService<IRepository<CommissionRule, Guid>>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _partnerTypeLookup = (TestCommissionPartnerTypeLookup)GetRequiredService<ICommissionPartnerTypeLookup>();
    }

    [Fact]
    public async Task Order_Paid_Ingest_Creates_Accrued_Commission()
    {
        var partnerId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPartnerRuleAsync(
                ruleId,
                partnerId,
                CommissionFeeType.Sale,
                CommissionDirection.PlatformEarns,
                percentageRate: 0.10m);

            await _ingestionService.IngestSnapshotAsync(CreatePaidSnapshot(
                partnerId,
                sourceOrderId: "paid-order-1",
                subtotal: 100m,
                taxAmount: 15m,
                deliveryFee: 20m,
                totalAmount: 135m));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var entries = await _ledgerRepository.GetListAsync();
            entries.Count.ShouldBe(1);

            var entry = entries.Single();
            entry.Status.ShouldBe(CommissionLedgerStatus.Accrued);
            entry.EntryKind.ShouldBe(CommissionEntryKind.Accrual);
            entry.BasisAmount.ShouldBe(100m);
            entry.ComputedCommission.ShouldBe(10.00m);
            entry.Direction.ShouldBe(CommissionDirection.PlatformEarns);
            entry.SourceType.ShouldBe(CommissionSourceTypes.OrderPaid);
        });
    }

    [Fact]
    public async Task Duplicate_Order_Ingest_Does_Not_Double_Accrue()
    {
        var partnerId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var snapshot = CreatePaidSnapshot(partnerId, "dup-order-1", 100m, 15m, 20m, 135m);

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPartnerRuleAsync(ruleId, partnerId, CommissionFeeType.Sale, CommissionDirection.PlatformEarns, 0.10m);

            var first = await _ingestionService.IngestSnapshotAsync(snapshot);
            first.IsNew.ShouldBeTrue();

            var second = await _ingestionService.IngestSnapshotAsync(snapshot);
            second.IsNew.ShouldBeFalse();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var entries = await _ledgerRepository.GetListAsync();
            entries.Count.ShouldBe(1);
        });
    }

    [Fact]
    public async Task Unpaid_Order_Ingest_Does_Not_Accrue()
    {
        var partnerId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPartnerRuleAsync(ruleId, partnerId, CommissionFeeType.Sale, CommissionDirection.PlatformEarns, 0.10m);

            await _ingestionService.IngestSnapshotAsync(CreateSnapshot(
                partnerId,
                sourceOrderId: "unpaid-order-1",
                subtotal: 100m,
                taxAmount: 15m,
                deliveryFee: 20m,
                totalAmount: 135m,
                paymentStatus: PaymentStatus.Unpaid,
                status: OrderStatus.Created));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            (await _ledgerRepository.GetListAsync()).ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task No_Subtotal_Or_Lines_Does_Not_Accrue_On_Total()
    {
        var partnerId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPartnerRuleAsync(ruleId, partnerId, CommissionFeeType.Sale, CommissionDirection.PlatformEarns, 0.10m);

            await _ingestionService.IngestSnapshotAsync(new OrderSourceSnapshot
            {
                SourceSystem = OrderLedgerConsts.FakeSourceSystem,
                SourceOrderId = "total-only-order-1",
                SourceVersion = 1,
                TenantId = Guid.NewGuid(),
                PartnerId = partnerId,
                Direction = OrderDirection.Outbound,
                Status = OrderStatus.Paid,
                PaymentStatus = PaymentStatus.Paid,
                Subtotal = 0m,
                TaxAmount = 15m,
                DeliveryFee = 20m,
                TotalAmount = 135m,
                SourceTimestamp = DateTime.UtcNow,
                Lines = []
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            (await _ledgerRepository.GetListAsync()).ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task PlatformEarns_And_PartnerEarns_Write_Correct_Direction()
    {
        var partnerId = Guid.NewGuid();
        var saleRuleId = Guid.NewGuid();
        var shipmentRuleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPartnerRuleAsync(
                saleRuleId,
                partnerId,
                CommissionFeeType.Sale,
                CommissionDirection.PlatformEarns,
                0.10m);

            await SeedPartnerRuleAsync(
                shipmentRuleId,
                partnerId,
                CommissionFeeType.Shipment,
                CommissionDirection.PartnerEarns,
                flatFee: 5m);

            await _ingestionService.IngestSnapshotAsync(CreatePaidSnapshot(
                partnerId,
                "direction-order-1",
                80m,
                12m,
                8m,
                100m));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var entries = await _ledgerRepository.GetListAsync();
            entries.Count.ShouldBe(2);

            entries.Single(x => x.Direction == CommissionDirection.PlatformEarns).ComputedCommission.ShouldBe(8.00m);
            entries.Single(x => x.Direction == CommissionDirection.PartnerEarns).ComputedCommission.ShouldBe(5.00m);
        });
    }

    [Fact]
    public async Task Overlapping_Sale_Rules_Create_One_Accrual_On_Paid_Ingest()
    {
        var partnerId = Guid.NewGuid();
        _partnerTypeLookup.SetPartnerType(partnerId, PartnerType.Aggregator);

        await WithUnitOfWorkAsync(async () =>
        {
            await _ruleRepository.InsertAsync(new CommissionRule(
                _guidGenerator.Create(),
                "Partner type sale rule",
                CommissionDirection.PlatformEarns,
                CommissionTriggerType.Sale,
                CommissionScopeKind.PartnerType,
                new CommissionBasisDefinition { PercentageRate = 0.10m },
                DateTime.UtcNow.AddDays(-1),
                CommissionFeeType.Sale,
                scopePartnerType: PartnerType.Aggregator), autoSave: true);

            await _ruleRepository.InsertAsync(new CommissionRule(
                _guidGenerator.Create(),
                "Partner sale rule",
                CommissionDirection.PlatformEarns,
                CommissionTriggerType.Sale,
                CommissionScopeKind.Partner,
                new CommissionBasisDefinition { PercentageRate = 0.15m },
                DateTime.UtcNow.AddDays(-1),
                CommissionFeeType.Sale,
                scopePartnerId: partnerId), autoSave: true);

            await _ingestionService.IngestSnapshotAsync(CreatePaidSnapshot(
                partnerId,
                "overlap-order-1",
                100m,
                15m,
                20m,
                135m));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var entries = await _ledgerRepository.GetListAsync();
            entries.Count.ShouldBe(1);
            entries.Single().ComputedCommission.ShouldBe(15.00m);
        });
    }

    [Fact]
    public async Task Distinct_Fee_Types_Both_Accrue_On_Paid_Ingest()
    {
        var partnerId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPartnerRuleAsync(
                _guidGenerator.Create(),
                partnerId,
                CommissionFeeType.Sale,
                CommissionDirection.PlatformEarns,
                0.10m);

            await SeedPartnerRuleAsync(
                _guidGenerator.Create(),
                partnerId,
                CommissionFeeType.Shipment,
                CommissionDirection.PartnerEarns,
                flatFee: 4m);

            await _ingestionService.IngestSnapshotAsync(CreatePaidSnapshot(
                partnerId,
                "fee-types-order-1",
                100m,
                15m,
                20m,
                135m));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var entries = await _ledgerRepository.GetListAsync();
            entries.Count.ShouldBe(2);
            entries.Select(x => x.RuleId).Distinct().Count().ShouldBe(2);
            entries.Select(x => x.ComputedCommission).OrderBy(x => x).ShouldBe([4.00m, 10.00m]);
        });
    }

    private async Task SeedPartnerRuleAsync(
        Guid ruleId,
        Guid partnerId,
        CommissionFeeType feeType,
        CommissionDirection direction,
        decimal? percentageRate = null,
        decimal? flatFee = null)
    {
        var basis = new CommissionBasisDefinition();
        if (percentageRate.HasValue)
        {
            basis = new CommissionBasisDefinition { PercentageRate = percentageRate.Value };
        }
        else if (flatFee.HasValue)
        {
            basis = new CommissionBasisDefinition { FlatFee = flatFee.Value };
        }

        await _ruleRepository.InsertAsync(new CommissionRule(
            ruleId,
            $"Rule-{feeType}",
            direction,
            CommissionTriggerType.Sale,
            CommissionScopeKind.Partner,
            basis,
            DateTime.UtcNow.AddDays(-1),
            feeType,
            scopePartnerId: partnerId), autoSave: true);
    }

    private static OrderSourceSnapshot CreatePaidSnapshot(
        Guid partnerId,
        string sourceOrderId,
        decimal subtotal,
        decimal taxAmount,
        decimal deliveryFee,
        decimal totalAmount) =>
        CreateSnapshot(
            partnerId,
            sourceOrderId,
            subtotal,
            taxAmount,
            deliveryFee,
            totalAmount,
            PaymentStatus.Paid,
            OrderStatus.Paid);

    private static OrderSourceSnapshot CreateSnapshot(
        Guid partnerId,
        string sourceOrderId,
        decimal subtotal,
        decimal taxAmount,
        decimal deliveryFee,
        decimal totalAmount,
        PaymentStatus paymentStatus,
        OrderStatus status) =>
        new()
        {
            SourceSystem = OrderLedgerConsts.FakeSourceSystem,
            SourceOrderId = sourceOrderId,
            SourceVersion = 1,
            TenantId = Guid.NewGuid(),
            PartnerId = partnerId,
            Direction = OrderDirection.Outbound,
            Status = status,
            PaymentStatus = paymentStatus,
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
