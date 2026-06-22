using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Track B — per-bank balances + 1100 roll-up. Each 110x sub-account is reported separately; the parent
/// total = direct (un-routed) + Σ children, and the trial balance still nets to zero.
/// </summary>
public class BankLedgerReportTests
{
    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 6);
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly Guid Merchant = Guid.NewGuid();
    private const decimal Vat = 0.15m;

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static PostingResult Principal(decimal sell, decimal buy, string orderRef) =>
        SettlementPostingTemplates.Principal(Incl(sell), Incl(buy), Vat).Tag(Partner, Merchant, Period, orderRef);

    private static PostingResult PayToBank(decimal amount, string? bankCode, string orderRef) =>
        SettlementPostingTemplates.PaymentReceived(Incl(amount), PaymentPayer.Merchant, bankCode)
            .Tag(Partner, Merchant, Period, orderRef);

    [Fact]
    public void Per_Bank_Balances_Are_Reported_Separately_And_Roll_Up_To_The_Parent_Total()
    {
        // Two orders fully paid into two different banks.
        var entries = new[]
        {
            Principal(60.00m, 40.00m, "ORD-1"),
            Principal(40.00m, 25.00m, "ORD-2"),
            PayToBank(60.00m, "1101", "ORD-1"),
            PayToBank(40.00m, "1102", "ORD-2"),
        };

        var report = SettlementReports.BankLedger(entries, Period);

        report.PerBank.Count.ShouldBe(2);
        report.PerBank.Select(b => b.AccountCode).ShouldBe(new[] { "1101", "1102" });
        report.PerBank.Single(b => b.AccountCode == "1101").Debit.ShouldBe(60.00m);
        report.PerBank.Single(b => b.AccountCode == "1102").Debit.ShouldBe(40.00m);

        // No un-routed cash → the parent's direct balance is zero…
        report.ParentDirect.Debit.ShouldBe(0m);
        // …and the roll-up (parent direct + Σ children) equals the total cash received.
        report.RollupDebits.Amount.ShouldBe(100.00m);
        report.RollupNet.ShouldBe(100.00m);

        // Parent = sum of children (the defining roll-up invariant).
        var childrenSum = report.PerBank.Sum(b => b.Debit) + report.ParentDirect.Debit;
        report.RollupDebits.Amount.ShouldBe(childrenSum);
    }

    [Fact]
    public void Un_Routed_Cash_Lands_On_The_1100_Parent_And_Still_Rolls_Up()
    {
        var entries = new[]
        {
            Principal(60.00m, 40.00m, "ORD-1"),
            Principal(25.00m, 10.00m, "ORD-2"),
            PayToBank(60.00m, "1101", "ORD-1"),
            PayToBank(25.00m, null, "ORD-2"), // no bank chosen → 1100 parent direct
        };

        var report = SettlementReports.BankLedger(entries, Period);

        report.PerBank.Single().AccountCode.ShouldBe("1101");
        report.ParentDirect.Debit.ShouldBe(25.00m);
        report.RollupDebits.Amount.ShouldBe(85.00m); // 60 (1101) + 25 (parent)
    }

    [Fact]
    public void Bank_Sliced_Postings_Keep_The_Trial_Balance_Balanced()
    {
        var entries = new[]
        {
            Principal(60.00m, 40.00m, "ORD-1"),
            Principal(40.00m, 25.00m, "ORD-2"),
            PayToBank(60.00m, "1101", "ORD-1"),
            PayToBank(40.00m, "1102", "ORD-2"),
        };

        var trial = SettlementReports.TrialBalanceFor(entries, Period);
        trial.IsBalanced.ShouldBeTrue();
        trial.Net.ShouldBe(0m);

        // The bank cash total in the trial balance equals the per-bank roll-up (no money invented/lost).
        var bankRows = trial.Accounts.Where(a => BankLedgerCoding.IsBankSubAccount(a.AccountCode)).Sum(a => a.Debit);
        bankRows.ShouldBe(SettlementReports.BankLedger(entries, Period).RollupDebits.Amount);
    }
}
