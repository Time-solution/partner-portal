using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Server-side enforcement of the FOUR-way split + COD invariants (ported from the frontend types.ts).
/// Worked numbers: 113 COD collected − 10 delivery = 103 net transferred; a 70-buy/100-sell resale leg →
/// 26.09 Zahy margin + 3.91 net VAT.
/// </summary>
public class FourWaySplitInvariantTests
{
    private static Money Sar(decimal amount) => Money.Of(amount, "SAR", vatInclusive: true);

    [Fact]
    public void Resale_70_buy_100_sell_yields_margin_26_09_and_net_vat_3_91()
    {
        // Resale leg: partner buy 70 (incl), sell/collected 100 (incl), no merchant pass-through.
        var split = SettlementFourWaySplit.DeriveFromFlow(
            collected: Sar(100m), merchantItems: Sar(0m), partnerBuyGross: Sar(70m), vatRate: 0.15m);

        split.ZahyMargin.Amount.ShouldBe(26.09m); // 86.96 sellNet − 60.87 buyNet
        split.NetVat.Amount.ShouldBe(3.91m);       // 13.04 outputVat − 9.13 inputVat
        split.Merchant.Amount.ShouldBe(0m);
        split.Delivery.Amount.ShouldBe(70.00m);
        split.ComponentsTotal().ShouldBe(100.00m);

        Should.NotThrow(() => split.EnsureBalances(Sar(100m)));
    }

    [Fact]
    public void Four_way_split_balances_collected_113_as_100_plus_10_plus_2_60_plus_0_40()
    {
        var split = SettlementFourWaySplit.DeriveFromFlow(
            collected: Sar(113m), merchantItems: Sar(100m), partnerBuyGross: Sar(10m), vatRate: 0.15m);

        split.Merchant.Amount.ShouldBe(100.00m);
        split.Delivery.Amount.ShouldBe(10.00m);
        split.ZahyMargin.Amount.ShouldBe(2.60m);
        split.NetVat.Amount.ShouldBe(0.40m);
        split.ComponentsTotal().ShouldBe(113.00m);

        Should.NotThrow(() => split.EnsureBalances(Sar(113m)));
    }

    [Fact]
    public void Imbalanced_split_throws_with_the_explicit_merchant_leg_named()
    {
        // 100 + 10 + 2.60 + 0.50 = 113.10 ≠ 113.00.
        var bad = new SettlementFourWaySplit(
            merchant: Sar(100m), delivery: Sar(10m), zahyMargin: Sar(2.60m), netVat: Sar(0.50m));

        var ex = Should.Throw<BusinessException>(() => bad.EnsureBalances(Sar(113m)));
        ex.Code.ShouldBe(SettlementSplitErrorCodes.FourWaySplitDoesNotBalance);
        ex.Data["Merchant"].ShouldBe(100.00m); // explicit merchant leg surfaced in the imbalance data
    }

    [Fact]
    public void Cod_113_collected_minus_10_delivery_transfers_103_and_reconciles()
    {
        var split = SettlementFourWaySplit.DeriveFromFlow(
            collected: Sar(113m), merchantItems: Sar(100m), partnerBuyGross: Sar(10m), vatRate: 0.15m);

        // collected − delivery = net transferred (113 − 10 = 103).
        SettlementFourWaySplit.CodNetTransferred(Sar(113m), Sar(10m)).ShouldBe(103.00m);
        // net transferred = Merchant + ZahyMargin + NetVAT (100 + 2.60 + 0.40 = 103); delivery is retained.
        split.RecipientsExcludingDelivery().ShouldBe(103.00m);

        Should.NotThrow(() => split.EnsureCodReconciles(Sar(113m), deliveryFee: Sar(10m), seededNetTransferred: Sar(103m)));
    }

    [Fact]
    public void Cod_rejects_a_hand_seeded_transfer_that_drifts_from_collected_minus_delivery()
    {
        var split = SettlementFourWaySplit.DeriveFromFlow(
            collected: Sar(113m), merchantItems: Sar(100m), partnerBuyGross: Sar(10m), vatRate: 0.15m);

        // The old bad seed: 95 instead of the computed 103 → an 8.00 silent gap, now rejected.
        var ex = Should.Throw<BusinessException>(
            () => split.EnsureCodReconciles(Sar(113m), deliveryFee: Sar(10m), seededNetTransferred: Sar(95m)));
        ex.Code.ShouldBe(SettlementSplitErrorCodes.CodNetTransferMismatch);
    }

    [Fact]
    public void Allocator_marketplace_output_satisfies_the_same_canonical_four_way_invariant()
    {
        var input = new SettlementAllocationInput
        {
            Book = SettlementBook.Marketplace,
            PartnerId = Guid.NewGuid(),
            ExternalTransactionId = "txn-901",
            CollectedTotal = Money.Of(113m, "SAR"),
            MerchantPayout = Money.Of(100m, "SAR"),
            PlatformCommissionInclusive = Money.Of(3m, "SAR"), // 2.61 net + 0.39 VAT
            DeliveryCost = Money.Of(10m, "SAR"),
            VatRate = 0.15m
        };

        var result = new SettlementAllocator().Allocate(input, new AggregatorFlowProfile(), DateTime.UtcNow);

        // Reconstruct the canonical four-way split from the allocator's result and assert it balances.
        var split = new SettlementFourWaySplit(
            merchant: result.MerchantPayout,
            delivery: result.DeliveryCost,
            zahyMargin: result.PlatformCommissionNet,
            netVat: result.NetVatToZatca);
        Should.NotThrow(() => split.EnsureBalances(input.CollectedTotal));
        split.ComponentsTotal().ShouldBe(113.00m);
    }

    [Fact]
    public void Allocator_imbalanced_input_throws_through_the_unified_invariant()
    {
        var input = new SettlementAllocationInput
        {
            Book = SettlementBook.Marketplace,
            PartnerId = Guid.NewGuid(),
            ExternalTransactionId = "txn-bad",
            CollectedTotal = Money.Of(113m, "SAR"),
            MerchantPayout = Money.Of(100m, "SAR"),
            PlatformCommissionInclusive = Money.Of(3m, "SAR"),
            DeliveryCost = Money.Of(5m, "SAR"), // 100 + 2.61 + 0.39 + 5 = 108 ≠ 113
            VatRate = 0.15m
        };

        Should.Throw<BusinessException>(
                () => new SettlementAllocator().Allocate(input, new AggregatorFlowProfile(), DateTime.UtcNow))
            .Code.ShouldBe(SettlementWebhookErrorCodes.AllocationDoesNotBalance);
    }
}
