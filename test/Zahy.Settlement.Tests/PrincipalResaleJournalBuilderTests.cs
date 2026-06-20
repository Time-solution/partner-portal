using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Accountant-approved principal delivery example (buy 10 / sell 13 incl VAT, round-per-line).
/// </summary>
public class PrincipalResaleJournalBuilderTests
{
    private const decimal Rate = 0.15m;
    private static readonly DateTime PostedAt = new(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ResaleVatCalculator Calc = new();

    [Fact]
    public void Delivery_Buy10_Sell13_Marketplace_BalancedPrincipalLegs()
    {
        var line = CostMarkupLine.Of(
            Money.Of(10m, vatInclusive: true),
            Money.Of(13m, vatInclusive: true));
        var vat = Calc.Compute(line, Rate, VatTreatment.Principal);

        vat.InputVat.Amount.ShouldBe(1.30m);
        vat.OutputVat.Amount.ShouldBe(1.70m);
        vat.NetVatToZatca.Amount.ShouldBe(0.40m);
        vat.Margin.Amount.ShouldBe(2.60m);

        var journal = PrincipalResaleJournalBuilder.Build(
            line,
            vat,
            SettlementBook.Marketplace,
            PostedAt,
            Guid.NewGuid(),
            "delivery:ord-1");

        journal.IsBalanced.ShouldBeTrue();
        journal.TotalDebits.Amount.ShouldBe(journal.TotalCredits.Amount);
        SumLeg(journal, SettlementAccountType.VatOutput, EntryDirection.Credit).ShouldBe(1.70m);
        SumLeg(journal, SettlementAccountType.VatInput, EntryDirection.Debit).ShouldBe(1.30m);
        SumLeg(journal, SettlementAccountType.ShippingMarginRevenue, EntryDirection.Credit).ShouldBe(2.60m);
    }

    [Fact]
    public void Reverse_Flips_All_Principal_Legs_And_Nets_To_Zero()
    {
        var line = CostMarkupLine.Of(
            Money.Of(10m, vatInclusive: true),
            Money.Of(13m, vatInclusive: true));
        var vat = Calc.Compute(line, Rate, VatTreatment.Principal);
        var original = PrincipalResaleJournalBuilder.Build(
            line,
            vat,
            SettlementBook.Marketplace,
            PostedAt,
            Guid.NewGuid());

        var reversal = original.Reverse(Guid.NewGuid(), PostedAt);

        reversal.IsBalanced.ShouldBeTrue();
        NetAccount(original, reversal, SettlementAccountType.VatOutput).ShouldBe(0m);
        NetAccount(original, reversal, SettlementAccountType.VatInput).ShouldBe(0m);
        NetAccount(original, reversal, SettlementAccountType.ShippingMarginRevenue).ShouldBe(0m);
    }

    private static decimal SumLeg(Journal journal, SettlementAccountType account, EntryDirection direction) =>
        journal.Lines
            .Where(l => l.Account == account && l.Direction == direction)
            .Sum(l => l.Amount.Amount);

    private static decimal NetAccount(Journal original, SettlementAccountType account) =>
        SumLeg(original, account, EntryDirection.Debit) - SumLeg(original, account, EntryDirection.Credit);

    private static decimal NetAccount(Journal original, Journal reversal, SettlementAccountType account) =>
        NetAccount(original, account) + NetAccount(reversal, account);
}
