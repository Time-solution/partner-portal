using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Per-partner ledger view — posting routes each partner's payable/receivable leg to its OWN sub-account
/// (2101+ under 2100, 1251+ under 1250), the parent equals direct + Σ children, and the trial balance
/// still nets to zero. Multi-3PL (Salasa + Oto on one merchant) settle on separate ledger lines that
/// each reconcile to the parent. Pure aggregation — no money math is changed.
/// </summary>
public class PartnerLedgerReportTests
{
    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 6);
    private static readonly Guid Salasa = Guid.NewGuid();
    private static readonly Guid Oto = Guid.NewGuid();
    private static readonly Guid Merchant = Guid.NewGuid();
    private const decimal Vat = 0.15m;

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static PostingResult Principal(Guid partner, decimal sell, decimal buy, string? apCode, string orderRef) =>
        SettlementPostingTemplates.Principal(Incl(sell), Incl(buy), Vat, apCode).Tag(partner, Merchant, Period, orderRef);

    private static PostingResult Disburse(Guid partner, decimal amount, string? apCode, string orderRef) =>
        SettlementPostingTemplates.Disbursement(Incl(amount), apCode).Tag(partner, Merchant, Period, orderRef);

    private static PostingResult PartnerFee(Guid partner, decimal fee, string? arCode, string orderRef) =>
        SettlementPostingTemplates.Fee(Incl(fee), ActivationFeePayer.Partner, Vat, arCode).Tag(partner, Merchant, Period, orderRef);

    private static PostingResult PartnerPays(Guid partner, decimal amount, string? arCode, string orderRef) =>
        SettlementPostingTemplates.PaymentReceived(Incl(amount), PaymentPayer.Partner, bankAccountCode: null, partnerArCode: arCode)
            .Tag(partner, Merchant, Period, orderRef);

    [Fact]
    public void Multi_3PL_Payable_Lands_On_Separate_Sub_Accounts_That_Roll_Up_To_2100()
    {
        // Salasa → 2101 (buy 40 incl), Oto → 2102 (buy 25 incl). Two 3PLs on one merchant.
        var entries = new[]
        {
            Principal(Salasa, 60.00m, 40.00m, "2101", "ORD-S"),
            Principal(Oto, 40.00m, 25.00m, "2102", "ORD-O"),
        };

        var report = SettlementReports.PartnerLedger(entries, Period);

        report.PerPartnerPayable.Count.ShouldBe(2);
        report.PerPartnerPayable.Select(a => a.AccountCode).ShouldBe(new[] { "2101", "2102" });
        // AP is credit-normal: each sub-account holds its own partner's payable.
        report.PerPartnerPayable.Single(a => a.AccountCode == "2101").Credit.ShouldBe(40.00m);
        report.PerPartnerPayable.Single(a => a.AccountCode == "2102").Credit.ShouldBe(25.00m);

        // Nothing un-routed → the 2100 parent's direct balance is zero…
        report.PayableParentDirect.Credit.ShouldBe(0m);
        // …and the roll-up (parent direct + Σ children) = total partner payable.
        report.PayableRollupCredits.Amount.ShouldBe(65.00m);
        report.PayableRollupNet.ShouldBe(65.00m);

        // Parent = sum of children (the defining roll-up invariant).
        var childrenSum = report.PerPartnerPayable.Sum(a => a.Credit) + report.PayableParentDirect.Credit;
        report.PayableRollupCredits.Amount.ShouldBe(childrenSum);
    }

    [Fact]
    public void Disbursing_A_Partner_Nets_Down_Its_Own_Sub_Account_And_The_Parent_Rollup()
    {
        var entries = new[]
        {
            Principal(Salasa, 60.00m, 40.00m, "2101", "ORD-S"),
            Principal(Oto, 40.00m, 25.00m, "2102", "ORD-O"),
            Disburse(Salasa, 40.00m, "2101", "PAY-S"), // pay Salasa down to zero
        };

        var report = SettlementReports.PartnerLedger(entries, Period);

        // Salasa's 2101 nets to zero (credit 40 − debit 40); Oto's 2102 still owes 25.
        report.PerPartnerPayable.Single(a => a.AccountCode == "2101").Net.ShouldBe(0m); // debit − credit = 40 − 40
        report.PerPartnerPayable.Single(a => a.AccountCode == "2102").Net.ShouldBe(-25.00m);

        // Roll-up net payable now = just Oto's outstanding 25.
        report.PayableRollupNet.ShouldBe(25.00m);
    }

    [Fact]
    public void Partner_Receivable_Routes_To_The_1251_Sub_Account_And_Nets_Down_On_Payment()
    {
        // Bill Salasa a partner-side fee → Dr 1251; then Salasa pays → Cr 1251 (nets to zero).
        var entries = new[]
        {
            PartnerFee(Salasa, 23.00m, "1251", "FEE-S"),
            PartnerPays(Salasa, 23.00m, "1251", "RCPT-S"),
        };

        var report = SettlementReports.PartnerLedger(entries, Period);

        report.PerPartnerReceivable.Single().AccountCode.ShouldBe("1251");
        // AR is debit-normal: billed 23 (debit) then cleared 23 (credit) → net zero.
        report.PerPartnerReceivable.Single().Net.ShouldBe(0m);
        report.ReceivableRollupNet.ShouldBe(0m);
    }

    [Fact]
    public void Routed_Partner_Postings_Keep_The_Trial_Balance_Balanced()
    {
        var entries = new[]
        {
            Principal(Salasa, 60.00m, 40.00m, "2101", "ORD-S"),
            Principal(Oto, 40.00m, 25.00m, "2102", "ORD-O"),
            Disburse(Salasa, 40.00m, "2101", "PAY-S"),
            PartnerFee(Oto, 11.50m, "1252", "FEE-O"),
        };

        var trial = SettlementReports.TrialBalanceFor(entries, Period);
        trial.IsBalanced.ShouldBeTrue();
        trial.Net.ShouldBe(0m);

        // The partner payable rows in the trial balance reconcile to the per-partner roll-up
        // (no money invented or lost by routing the leg to a sub-account).
        var report = SettlementReports.PartnerLedger(entries, Period);
        var payableRows = trial.Accounts.Where(a => PartnerLedgerCoding.IsPayableSubAccount(a.AccountCode));
        payableRows.Sum(a => a.Credit).ShouldBe(report.PayableRollupCredits.Amount);
        payableRows.Sum(a => a.Debit).ShouldBe(report.PayableRollupDebits.Amount);
    }

    [Fact]
    public void Unrouted_Partner_Payable_Falls_Back_To_The_2100_Parent_And_Still_Rolls_Up()
    {
        var entries = new[]
        {
            Principal(Salasa, 60.00m, 40.00m, "2101", "ORD-S"),
            Principal(Oto, 25.00m, 10.00m, null, "ORD-O"), // no sub-account chosen → 2100 parent direct
        };

        var report = SettlementReports.PartnerLedger(entries, Period);

        report.PerPartnerPayable.Single().AccountCode.ShouldBe("2101");
        report.PayableParentDirect.Credit.ShouldBe(10.00m);
        report.PayableRollupCredits.Amount.ShouldBe(50.00m); // 40 (2101) + 10 (parent)
    }
}
