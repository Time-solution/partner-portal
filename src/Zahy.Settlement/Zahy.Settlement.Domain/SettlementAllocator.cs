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

        var sumOfParts = SettlementMoney.Round(payout + commissionNet + commissionVat + delivery);
        if (sumOfParts != SettlementMoney.Round(collected))
        {
            throw new BusinessException(SettlementWebhookErrorCodes.AllocationDoesNotBalance)
                .WithData("Collected", collected)
                .WithData("SumOfParts", sumOfParts);
        }

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
}
