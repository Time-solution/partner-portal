using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// F16 — the largest-remainder penny reconciliation must ALWAYS return parts that sum exactly to the
/// input total (2dp) without ever driving a leg negative, with deterministic lowest-index tie-breaking.
/// The single-cent-on-the-largest-leg behavior already pinned by SettlementAllocatorPennyResidualTests
/// is preserved byte-identically; only the previously-broken adversarial cases change (they crashed).
/// </summary>
public class SettlementPennyReconcilerTests
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

    [Fact]
    public void REPRO_TwoCent_Residual_On_Tiny_Legs_Must_Not_Produce_A_Negative_Allocation()
    {
        // Adversarial split: legs [0.01, 0.01, 0.01, 0.00] vs collected 0.01 → −0.02 residual, inside
        // the tolerance window. The old rule dumped BOTH cents on one leg (0.01 − 0.02 = −0.01): a
        // negative allocation from positive inputs, which then blew up journal construction. The fix
        // distributes one cent per step, never below zero.
        var result = new SettlementAllocator()
            .Allocate(Input(collected: 0.01m, payout: 0.01m, commissionInclusive: 0.01m, delivery: 0.01m, ext: "txn-tiny"), Profile, DateTime.UtcNow);

        result.MerchantPayout.Amount.ShouldBeGreaterThanOrEqualTo(0m);
        result.DeliveryCost.Amount.ShouldBeGreaterThanOrEqualTo(0m);
        result.PlatformCommissionNet.Amount.ShouldBeGreaterThanOrEqualTo(0m);
        result.NetVatToZatca.Amount.ShouldBeGreaterThanOrEqualTo(0m);

        (result.MerchantPayout.Amount + result.DeliveryCost.Amount +
         result.PlatformCommissionNet.Amount + result.NetVatToZatca.Amount)
            .ShouldBe(0.01m); // parts sum EXACTLY to the collected total

        result.Journal.TotalDebits.Amount.ShouldBe(0.01m);
        result.Journal.TotalCredits.Amount.ShouldBe(0.01m);
    }

    // ---- the reconciliation rule itself (n-way, property-style) --------------------------------

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(13)]
    public void Reconcile_NWay_Splits_Always_Sum_Exactly_To_The_Total(int ways)
    {
        // Odd, awkward totals divided into n unequal raw legs — the raw sum drifts from the total by
        // a few sub-cent remainders. After reconciliation: sum(parts) == total, all parts ≥ 0, 2dp.
        foreach (var total in new[] { 0.01m, 0.05m, 1.00m, 9.99m, 100.01m, 1234.57m })
        {
            var raw = new decimal[ways];
            for (var i = 0; i < ways; i++)
            {
                // Deterministic unequal shares of the total (weights 1..n), un-rounded on purpose.
                raw[i] = total * (i + 1) / (ways * (ways + 1) / 2m);
            }

            var parts = SettlementPennyReconciler.Reconcile(raw, total);

            parts.Sum().ShouldBe(SettlementMoney.Round(total), $"total {total} split {ways}-way");
            parts.ShouldAllBe(p => p >= 0m);
            parts.ShouldAllBe(p => p == SettlementMoney.Round(p)); // strictly 2dp
        }
    }

    [Fact]
    public void Reconcile_Equal_Remainders_Break_Ties_By_Lowest_Index_Deterministically()
    {
        // Four equal legs, −0.02 residual → exactly two legs give up one cent each, chosen from the
        // start of the array (deterministic ordering for equal remainders).
        var parts = SettlementPennyReconciler.Reconcile(new[] { 0.01m, 0.01m, 0.01m, 0.01m }, 0.02m);
        parts.ShouldBe(new[] { 0.00m, 0.00m, 0.01m, 0.01m });

        // …and byte-identical on every run (purity).
        for (var i = 0; i < 100; i++)
        {
            SettlementPennyReconciler.Reconcile(new[] { 0.01m, 0.01m, 0.01m, 0.01m }, 0.02m)
                .ShouldBe(new[] { 0.00m, 0.00m, 0.01m, 0.01m });
        }
    }

    [Fact]
    public void Reconcile_Single_Cent_Still_Lands_On_The_Largest_Leg_Unchanged()
    {
        // The behavior the allocator suite already pins: +0.01 goes to the largest-share leg.
        var parts = SettlementPennyReconciler.Reconcile(new[] { 100m, 10m, 2.61m, 0.39m }, 113.01m);
        parts.ShouldBe(new[] { 100.01m, 10.00m, 2.61m, 0.39m });
    }

    [Fact]
    public void Reconcile_Genuine_Imbalance_Is_Left_For_The_Caller_To_Reject()
    {
        // Residual beyond one cent per leg is NOT rounding — parts are returned rounded-unreconciled
        // so the four-way invariant still rejects, exactly as before.
        var parts = SettlementPennyReconciler.Reconcile(new[] { 100m, 10m, 2.61m, 0.39m }, 118.00m);
        parts.Sum().ShouldBe(113.00m); // untouched → caller's balance check throws
    }
}
