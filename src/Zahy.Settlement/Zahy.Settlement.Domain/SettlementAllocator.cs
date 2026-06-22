using System;
using System.Collections.Generic;
using Volo.Abp;

namespace Zahy.Settlement;

public interface ISettlementAllocator
{
    SettlementAllocationResult Allocate(SettlementAllocationInput input, ISettlementFlowProfile profile, DateTime postedAt);
}

/// <summary>
/// Builds a balanced journal for a collected settlement.
/// ⚠️ PROVISIONAL account-leg structure — pending the accountant's chart sign-off (DESIGN.md §11.6).
/// Not authoritative accounting. Marketplace (aggregator) is implemented for the worked example;
/// Integration (resale) allocation is intentionally gated until the chart is signed off.
/// </summary>
public sealed class SettlementAllocator : ISettlementAllocator
{
    public SettlementAllocationResult Allocate(SettlementAllocationInput input, ISettlementFlowProfile profile, DateTime postedAt)
    {
        Check.NotNull(input, nameof(input));
        Check.NotNull(profile, nameof(profile));

        return profile.Book switch
        {
            SettlementBook.Marketplace => AllocateMarketplace(input, profile, postedAt),
            _ => throw new BusinessException(SettlementWebhookErrorCodes.AllocationNotConfiguredForBook)
                .WithData("Book", profile.Book.ToString())
        };
    }

    private static SettlementAllocationResult AllocateMarketplace(SettlementAllocationInput input, ISettlementFlowProfile profile, DateTime postedAt)
    {
        var currency = input.CollectedTotal.Currency;

        var collected = input.CollectedTotal.Amount;
        var payout = input.MerchantPayout.Amount;
        var commissionInclusive = input.PlatformCommissionInclusive.Amount;
        var delivery = input.DeliveryCost.Amount;

        var commissionNet = VatMath.NetOfInclusive(commissionInclusive, input.VatRate);
        var commissionVat = SettlementMoney.Round(commissionInclusive - commissionNet);

        // F16 — round each leg ONCE, then push any sub-tolerance rounding residual onto the largest leg
        // (largest-remainder method) so the four legs sum byte-exactly to the collected total. A clean
        // (zero-residual) input is unchanged; a residual bigger than one cent per leg is a genuine
        // imbalance and is left for the four-way invariant below to reject.
        var legs = ReconcilePennyResidualToLargestLeg(
            new[] { payout, delivery, commissionNet, commissionVat },
            collected);
        payout = legs[0];
        delivery = legs[1];
        commissionNet = legs[2];
        commissionVat = legs[3];

        // ONE canonical invariant (ported from the FE): Collected = Merchant + Delivery + ZahyMargin + NetVAT.
        // For the aggregator there is no buy leg, so ZahyMargin = commission net and NetVAT = output VAT
        // (input VAT is zero). This replaces the allocator's previous bespoke balance check.
        var split = new SettlementFourWaySplit(
            merchant: Money.Of(payout, currency),
            delivery: Money.Of(delivery, currency),
            zahyMargin: Money.Of(commissionNet, currency),
            netVat: Money.Of(commissionVat, currency));
        split.EnsureBalances(input.CollectedTotal, SettlementWebhookErrorCodes.AllocationDoesNotBalance);

        var lines = new List<JournalLine>
        {
            JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Of(collected, currency))
        };
        AddCreditIfPositive(lines, SettlementAccountType.MerchantPayable, payout, currency);
        AddCreditIfPositive(lines, SettlementAccountType.PlatformCommissionRevenue, commissionNet, currency);
        AddCreditIfPositive(lines, SettlementAccountType.VatOutput, commissionVat, currency);
        AddCreditIfPositive(lines, SettlementAccountType.DeliveryCost, delivery, currency);

        var journal = Journal.Create(Guid.NewGuid(), postedAt, lines, $"settlement:{input.ExternalTransactionId}");
        BookIsolationGuard.EnsureWithinBook(profile, journal); // cross-book postings rejected

        return new SettlementAllocationResult
        {
            Journal = journal,
            MerchantPayout = Money.Of(payout, currency),
            PlatformCommissionNet = Money.Of(commissionNet, currency),
            DeliveryCost = Money.Of(delivery, currency),
            VatOutput = Money.Of(commissionVat, currency),
            VatInput = Money.Zero(currency),
            NetVatToZatca = Money.Of(commissionVat, currency)
        };
    }

    private static void AddCreditIfPositive(List<JournalLine> lines, SettlementAccountType account, decimal amount, string currency)
    {
        if (amount > 0m)
        {
            lines.Add(JournalLine.Credit(account, Money.Of(amount, currency)));
        }
    }

    /// <summary>
    /// Round each leg once, then reconcile any residual penny between the legs' total and the collected
    /// total onto the LARGEST-share leg (largest-remainder method — the standard, audit-friendly rule for
    /// distributing a rounding cent). Returns the legs unchanged when:
    ///   • the residual is zero (clean input — byte-identical to the previous per-leg rounding), or
    ///   • the residual exceeds one cent per leg (a genuine imbalance — left for the caller to reject).
    /// </summary>
    private static decimal[] ReconcilePennyResidualToLargestLeg(decimal[] legs, decimal collected)
    {
        var rounded = new decimal[legs.Length];
        var sum = 0m;
        for (var i = 0; i < legs.Length; i++)
        {
            rounded[i] = SettlementMoney.Round(legs[i]);
            sum += rounded[i];
        }

        var residual = SettlementMoney.Round(SettlementMoney.Round(collected) - sum);
        if (residual == 0m)
        {
            return rounded;
        }

        var pennyTolerance = 0.01m * legs.Length;
        if (Math.Abs(residual) > pennyTolerance)
        {
            return rounded;
        }

        var largest = 0;
        for (var i = 1; i < rounded.Length; i++)
        {
            if (rounded[i] > rounded[largest])
            {
                largest = i;
            }
        }

        rounded[largest] = SettlementMoney.Round(rounded[largest] + residual);
        return rounded;
    }
}
