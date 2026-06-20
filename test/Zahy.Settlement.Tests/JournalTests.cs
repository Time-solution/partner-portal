using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

public class JournalTests
{
    private static readonly DateTime PostedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Balanced_Two_Line_Journal_Is_Accepted()
    {
        var journal = Journal.Create(Guid.NewGuid(), PostedAt, new[]
        {
            JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Of(100m)),
            JournalLine.Credit(SettlementAccountType.MerchantPayable, Money.Of(100m))
        });

        journal.IsBalanced.ShouldBeTrue();
        journal.TotalDebits.Amount.ShouldBe(100m);
        journal.TotalCredits.Amount.ShouldBe(100m);
        journal.Lines.Count.ShouldBe(2);
        journal.Currency.ShouldBe("SAR");
    }

    [Fact]
    public void Multi_Line_Settlement_Split_Balances()
    {
        // Collected 100 split: merchant payout 80, platform commission 15, delivery cost 5.
        var journal = Journal.Create(Guid.NewGuid(), PostedAt, new[]
        {
            JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Of(100m)),
            JournalLine.Credit(SettlementAccountType.MerchantPayable, Money.Of(80m)),
            JournalLine.Credit(SettlementAccountType.PlatformCommissionRevenue, Money.Of(15m)),
            JournalLine.Credit(SettlementAccountType.DeliveryCost, Money.Of(5m))
        });

        journal.IsBalanced.ShouldBeTrue();
        journal.TotalDebits.Amount.ShouldBe(100m);
        journal.TotalCredits.Amount.ShouldBe(100m);
    }

    [Fact]
    public void Unbalanced_Journal_Is_Rejected()
    {
        Should.Throw<BusinessException>(() => Journal.Create(Guid.NewGuid(), PostedAt, new[]
        {
            JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Of(100m)),
            JournalLine.Credit(SettlementAccountType.MerchantPayable, Money.Of(99.99m))
        })).Code.ShouldBe(SettlementErrorCodes.UnbalancedJournal);
    }

    [Fact]
    public void Single_Line_Journal_Is_Rejected()
    {
        Should.Throw<BusinessException>(() => Journal.Create(Guid.NewGuid(), PostedAt, new[]
        {
            JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Of(100m))
        })).Code.ShouldBe(SettlementErrorCodes.DegenerateJournal);
    }

    [Fact]
    public void Multi_Currency_Journal_Is_Rejected()
    {
        Should.Throw<BusinessException>(() => Journal.Create(Guid.NewGuid(), PostedAt, new[]
        {
            JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Of(100m, "SAR")),
            JournalLine.Credit(SettlementAccountType.MerchantPayable, Money.Of(100m, "USD"))
        })).Code.ShouldBe(SettlementErrorCodes.CurrencyMismatch);
    }

    [Fact]
    public void Zero_Or_Negative_Line_Amount_Is_Rejected()
    {
        Should.Throw<BusinessException>(() =>
                JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Zero()))
            .Code.ShouldBe(SettlementErrorCodes.NonPositiveAmount);

        Should.Throw<BusinessException>(() =>
                JournalLine.Credit(SettlementAccountType.MerchantPayable, Money.Of(-5m)))
            .Code.ShouldBe(SettlementErrorCodes.NonPositiveAmount);
    }

    [Fact]
    public void Reverse_Flips_Directions_And_Stays_Balanced()
    {
        var original = Journal.Create(Guid.NewGuid(), PostedAt, new[]
        {
            JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Of(100m)),
            JournalLine.Credit(SettlementAccountType.MerchantPayable, Money.Of(80m)),
            JournalLine.Credit(SettlementAccountType.PlatformCommissionRevenue, Money.Of(20m))
        });

        var reversal = original.Reverse(Guid.NewGuid(), PostedAt);

        reversal.IsBalanced.ShouldBeTrue();
        reversal.ReversesJournalId.ShouldBe(original.Id);
        reversal.TotalDebits.Amount.ShouldBe(original.TotalCredits.Amount);
        reversal.TotalCredits.Amount.ShouldBe(original.TotalDebits.Amount);
    }
}
