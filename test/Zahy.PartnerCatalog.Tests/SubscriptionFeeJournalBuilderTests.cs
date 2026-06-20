using System;
using System.Linq;
using Shouldly;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class SubscriptionFeeJournalBuilderTests
{
    private static readonly DateTime PostedAt = new(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Monthly_Fee_115_Incl_Posts_Output_Vat_Only_Balanced()
    {
        var journal = SubscriptionFeeJournalBuilder.Build(
            Money.Of(115m, vatInclusive: true),
            0.15m,
            PostedAt,
            Guid.NewGuid());

        journal.IsBalanced.ShouldBeTrue();
        journal.TotalDebits.Amount.ShouldBe(115m);
        journal.TotalCredits.Amount.ShouldBe(115m);

        SumLeg(journal, SettlementAccountType.VatOutput, EntryDirection.Credit).ShouldBe(15m);
        SumLeg(journal, SettlementAccountType.PlatformCommissionRevenue, EntryDirection.Credit).ShouldBe(100m);
        SumLeg(journal, SettlementAccountType.VatInput, EntryDirection.Debit).ShouldBe(0m);
    }

    private static decimal SumLeg(Journal journal, SettlementAccountType account, EntryDirection direction) =>
        journal.Lines
            .Where(l => l.Account == account && l.Direction == direction)
            .Sum(l => l.Amount.Amount);
}
