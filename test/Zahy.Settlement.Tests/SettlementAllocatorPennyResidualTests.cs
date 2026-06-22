using System;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// F16 — the marketplace allocator rounds each leg once and reconciles a sub-tolerance rounding residual
/// onto the largest-share leg (largest-remainder), instead of letting a 0.01-per-leg mismatch trip the
/// "doesn't balance" invariant. Clean inputs are unchanged; genuine imbalances still throw.
/// </summary>
public class SettlementAllocatorPennyResidualTests
{
    private static readonly AggregatorFlowProfile Profile = new();

    private static SettlementAllocationInput Input(
        decimal collected, decimal payout, decimal commissionInclusive, decimal delivery, string ext) =>
        new()
        {
            Book = SettlementBook.Marketplace,
            PartnerId = Guid.NewGuid(),
            ExternalTransactionId = ext,
            CollectedTotal = Money.Of(collected, "SAR"),
            MerchantPayout = Money.Of(payout, "SAR"),
            PlatformCommissionInclusive = Money.Of(commissionInclusive, "SAR"),
            DeliveryCost = Money.Of(delivery, "SAR"),
            VatRate = 0.15m
        };

    private static SettlementAllocationResult Allocate(SettlementAllocationInput input) =>
        new SettlementAllocator().Allocate(input, Profile, DateTime.UtcNow);

    [Fact]
    public void Allocator_PennyResidual_AbsorbedByLargestLeg()
    {
        // Legs round to 100 + 10 + 2.61 + 0.39 = 113.00, but collected is 113.01 → a +0.01 residual.
        // Previously this tripped AllocationDoesNotBalance; now it lands on the largest leg (merchant 100).
        var result = Allocate(Input(collected: 113.01m, payout: 100m, commissionInclusive: 3m, delivery: 10m, ext: "txn-penny"));

        result.MerchantPayout.Amount.ShouldBe(100.01m);     // residual absorbed by the largest-share leg
        result.DeliveryCost.Amount.ShouldBe(10.00m);         // other legs untouched
        result.PlatformCommissionNet.Amount.ShouldBe(2.61m);
        result.NetVatToZatca.Amount.ShouldBe(0.39m);

        // The journal balances byte-exactly against the collected total.
        result.Journal.TotalDebits.Amount.ShouldBe(113.01m);
        result.Journal.TotalCredits.Amount.ShouldBe(113.01m);
    }

    [Fact]
    public void Allocator_ZeroResidual_NoChangeFromCurrentBehavior()
    {
        // Clean input (113 = 100 + 10 + 2.61 + 0.39): no residual, so the legs are identical to before.
        var result = Allocate(Input(collected: 113.00m, payout: 100m, commissionInclusive: 3m, delivery: 10m, ext: "txn-clean"));

        result.MerchantPayout.Amount.ShouldBe(100.00m);
        result.DeliveryCost.Amount.ShouldBe(10.00m);
        result.PlatformCommissionNet.Amount.ShouldBe(2.61m);
        result.NetVatToZatca.Amount.ShouldBe(0.39m);
        result.Journal.TotalDebits.Amount.ShouldBe(113.00m);
        result.Journal.TotalCredits.Amount.ShouldBe(113.00m);
    }

    [Fact]
    public void Allocator_RepeatedAllocations_DoNotAccumulateDrift()
    {
        // Allocation is pure/stateless — 1000 runs on a residual-producing input give the SAME numbers
        // each time, so there is no accumulated drift. The largest leg always carries the single cent.
        for (var i = 0; i < 1000; i++)
        {
            var result = Allocate(Input(collected: 113.01m, payout: 100m, commissionInclusive: 3m, delivery: 10m, ext: $"txn-loop-{i}"));

            result.MerchantPayout.Amount.ShouldBe(100.01m);
            result.Journal.TotalDebits.Amount.ShouldBe(113.01m);
            result.Journal.TotalCredits.Amount.ShouldBe(result.Journal.TotalDebits.Amount);
        }
    }

    [Fact]
    public void Allocator_GenuineImbalance_StillThrows()
    {
        // Off by 5.00 (delivery 5 → legs total 108.00 vs 113.00) is well beyond a rounding cent and must
        // still be rejected — the penny tolerance does not mask a real imbalance.
        Should.Throw<Volo.Abp.BusinessException>(
                () => Allocate(Input(collected: 113.00m, payout: 100m, commissionInclusive: 3m, delivery: 5m, ext: "txn-imbalance")))
            .Code.ShouldBe(SettlementWebhookErrorCodes.AllocationDoesNotBalance);
    }
}
