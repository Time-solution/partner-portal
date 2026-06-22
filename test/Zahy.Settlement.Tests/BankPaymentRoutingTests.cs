using System;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Track B — manual payment routing. When the accountant picks a bank, the receipt debits that bank's
/// 110x sub-account instead of the generic 1100; with no bank chosen it falls back to 1100 (back-compat).
/// VAT/margin still untouched (a receipt only nets AR down) and the trial balance still nets to zero.
/// COMPUTE ONLY — PostingEnabled stays OFF.
/// </summary>
public class BankPaymentRoutingTests
{
    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 6);
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly Guid Merchant = Guid.NewGuid();
    private const decimal Vat = 0.15m;

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static decimal Line(PostingResult r, string code, EntryDirection dir) =>
        r.LineFor(code, dir).ShouldNotBeNull().Amount.Amount;

    [Fact]
    public void Payment_To_A_Chosen_Bank_Posts_Dr110x_Cr1200()
    {
        var journal = SettlementPostingTemplates.PaymentReceived(Incl(100.00m), PaymentPayer.Merchant, "1101");

        Line(journal, "1101", EntryDirection.Debit).ShouldBe(100.00m);
        Line(journal, SettlementAccountCode.ArMerchant, EntryDirection.Credit).ShouldBe(100.00m);
        // The generic 1100 parent is NOT used when a bank is chosen.
        journal.LineFor(SettlementAccountCode.BankCashClearing, EntryDirection.Debit).ShouldBeNull();

        journal.Margin.Amount.ShouldBe(0m);
        journal.NetVat.Amount.ShouldBe(0m);
        journal.IsBalanced.ShouldBeTrue();
        journal.TrialBalanceNet.ShouldBe(0m);
    }

    [Fact]
    public void Partner_Payment_To_A_Bank_Posts_Dr110x_Cr1250()
    {
        var journal = SettlementPostingTemplates.PaymentReceived(Incl(5.00m), PaymentPayer.Partner, "1102");

        Line(journal, "1102", EntryDirection.Debit).ShouldBe(5.00m);
        Line(journal, SettlementAccountCode.ArPartner, EntryDirection.Credit).ShouldBe(5.00m);
        journal.LineFor(SettlementAccountCode.ArMerchant, EntryDirection.Credit).ShouldBeNull();
    }

    [Fact]
    public void No_Bank_Chosen_Falls_Back_To_1100()
    {
        var journal = SettlementPostingTemplates.PaymentReceived(Incl(60.00m), PaymentPayer.Merchant, bankAccountCode: null);

        Line(journal, SettlementAccountCode.BankCashClearing, EntryDirection.Debit).ShouldBe(60.00m);
        Line(journal, SettlementAccountCode.ArMerchant, EntryDirection.Credit).ShouldBe(60.00m);
    }

    [Fact]
    public void A_NonBank_Code_Also_Falls_Back_To_1100_Safely()
    {
        // Not a reserved bank sub-code (1101–1149) → routed to the parent, never to an arbitrary account.
        var journal = SettlementPostingTemplates.PaymentReceived(Incl(40.00m), PaymentPayer.Merchant, "9999");

        Line(journal, SettlementAccountCode.BankCashClearing, EntryDirection.Debit).ShouldBe(40.00m);
    }

    [Fact]
    public void The_Old_Two_Arg_Overload_Still_Behaves_As_Before()
    {
        var journal = SettlementPostingTemplates.PaymentReceived(Incl(100.00m), PaymentPayer.Merchant);
        Line(journal, SettlementAccountCode.BankCashClearing, EntryDirection.Debit).ShouldBe(100.00m);
        Line(journal, SettlementAccountCode.ArMerchant, EntryDirection.Credit).ShouldBe(100.00m);
    }

    [Fact]
    public void Bank_Routed_Payment_Keeps_The_Trial_Balance_Net_Zero()
    {
        // Principal 70→100 books AR-Merchant (1200) = 100.00 debit.
        var principal = SettlementPostingTemplates.Principal(Incl(100.00m), Incl(70.00m), Vat)
            .Tag(Partner, Merchant, Period, "ORD-1");

        // Pay the full 100 into bank 1101: Dr 1101 / Cr 1200.
        var pay = SettlementPostingTemplates.PaymentReceived(Incl(100.00m), PaymentPayer.Merchant, "1101")
            .Tag(Partner, Merchant, Period, "ORD-1");

        var entries = new[] { principal, pay };
        var trial = SettlementReports.TrialBalanceFor(entries, Period);
        trial.IsBalanced.ShouldBeTrue();
        trial.Net.ShouldBe(0m);

        // AR-Merchant fully cleared; the cash now sits in the 1101 sub-account.
        SettlementReports.Merchant(entries, Merchant, Period).TotalReceivable.Amount.ShouldBe(0m);
    }
}
