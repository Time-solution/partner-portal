using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Phase C½/D — payment-received only (money IN). A balance (AR position) may receive partial and
/// multiple payments over time; each posts Dr 1100 Bank/Cash / Cr 1200|1250 AR for its amount.
/// Anchored to the existing mock numbers: principal 70→100 (AR 1200 = 100.00) and the Chefz partner
/// bulk invoice (AR 1250 = 5.00). Posting stays COMPUTE-ONLY — PostingEnabled is OFF, nothing disbursed.
/// </summary>
public class PaymentReceivedTests
{
    private const decimal Vat = 0.15m;
    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 6);
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly Guid Merchant = Guid.NewGuid();

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static decimal Line(PostingResult r, string code, EntryDirection dir) =>
        r.LineFor(code, dir).ShouldNotBeNull().Amount.Amount;

    private static Payment Pay(string againstRef, PaymentPayer payer, decimal amount) =>
        Payment.Record(Guid.NewGuid(), againstRef, payer, payer == PaymentPayer.Partner ? Partner : Merchant, Incl(amount), DateTime.UtcNow);

    // ── Posting template ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Merchant_Payment_Posts_Dr1100_Cr1200_For_Full_Amount()
    {
        var journal = SettlementPostingTemplates.PaymentReceived(Incl(100.00m), PaymentPayer.Merchant);

        Line(journal, SettlementAccountCode.BankCashClearing, EntryDirection.Debit).ShouldBe(100.00m);
        Line(journal, SettlementAccountCode.ArMerchant, EntryDirection.Credit).ShouldBe(100.00m);
        journal.LineFor(SettlementAccountCode.ArPartner, EntryDirection.Credit).ShouldBeNull();

        // A cash receipt earns no margin and recognises no VAT.
        journal.Margin.Amount.ShouldBe(0m);
        journal.NetVat.Amount.ShouldBe(0m);
        journal.IsBalanced.ShouldBeTrue();
        journal.TrialBalanceNet.ShouldBe(0m);
    }

    [Fact]
    public void Partner_Payment_Posts_Dr1100_Cr1250()
    {
        var journal = SettlementPostingTemplates.PaymentReceived(Incl(5.00m), PaymentPayer.Partner);

        Line(journal, SettlementAccountCode.BankCashClearing, EntryDirection.Debit).ShouldBe(5.00m);
        Line(journal, SettlementAccountCode.ArPartner, EntryDirection.Credit).ShouldBe(5.00m);
        journal.LineFor(SettlementAccountCode.ArMerchant, EntryDirection.Credit).ShouldBeNull();
        journal.IsBalanced.ShouldBeTrue();
    }

    // ── Remaining read model + state transitions ─────────────────────────────────────────────────

    [Fact]
    public void Full_Payment_Of_100_Leaves_Zero_Remaining_And_Paid()
    {
        var payments = new[] { Pay("STL-1", PaymentPayer.Merchant, 100.00m) };

        var status = PaymentLedger.Status("STL-1", Incl(100.00m), payments);

        status.PaidToDate.Amount.ShouldBe(100.00m);
        status.Remaining.Amount.ShouldBe(0m);
        status.State.ShouldBe(PaymentState.Paid);
    }

    [Fact]
    public void No_Payments_Is_Allocated_With_Full_Remaining()
    {
        var status = PaymentLedger.Status("STL-1", Incl(100.00m), Array.Empty<Payment>());

        status.PaidToDate.Amount.ShouldBe(0m);
        status.Remaining.Amount.ShouldBe(100.00m);
        status.State.ShouldBe(PaymentState.Allocated);
    }

    [Fact]
    public void Partial_60_Then_40_Walks_Allocated_PartiallyPaid_Paid()
    {
        var ar = Incl(100.00m);

        var first = Pay("STL-1", PaymentPayer.Merchant, 60.00m);
        var afterFirst = PaymentLedger.Status("STL-1", ar, new[] { first });
        afterFirst.Remaining.Amount.ShouldBe(40.00m);
        afterFirst.State.ShouldBe(PaymentState.PartiallyPaid);

        var second = Pay("STL-1", PaymentPayer.Merchant, 40.00m);
        var afterSecond = PaymentLedger.Status("STL-1", ar, new[] { first, second });
        afterSecond.Remaining.Amount.ShouldBe(0m);
        afterSecond.State.ShouldBe(PaymentState.Paid);
    }

    [Fact]
    public void Partner_Bulk_Invoice_Pay_5_Clears_To_Zero_Paid()
    {
        var payments = new[] { Pay("INV-CHEFZ-2026-06", PaymentPayer.Partner, 5.00m) };

        var status = PaymentLedger.Status("INV-CHEFZ-2026-06", Incl(5.00m), payments);

        status.PaidToDate.Amount.ShouldBe(5.00m);
        status.Remaining.Amount.ShouldBe(0m);
        status.State.ShouldBe(PaymentState.Paid);
    }

    // ── Overpayment is rejected ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Overpayment_Pay_120_On_100_Balance_Is_Rejected()
    {
        var ex = Should.Throw<BusinessException>(() =>
            PaymentLedger.EnsureWithinRemaining(Incl(100.00m), paidToDate: 0m, Incl(120.00m)));

        ex.Code.ShouldBe(SettlementPaymentErrorCodes.Overpayment);
    }

    [Fact]
    public void Overpayment_Across_Multiple_Payments_Is_Rejected()
    {
        // 70 already received against a 100 balance — a further 40 would exceed remaining (30).
        var ex = Should.Throw<BusinessException>(() =>
            PaymentLedger.EnsureWithinRemaining(Incl(100.00m), paidToDate: 70.00m, Incl(40.00m)));

        ex.Code.ShouldBe(SettlementPaymentErrorCodes.Overpayment);
    }

    [Fact]
    public void Exact_Remaining_Is_Accepted()
    {
        // Paying exactly the remaining 30 is allowed (not an overpayment).
        Should.NotThrow(() =>
            PaymentLedger.EnsureWithinRemaining(Incl(100.00m), paidToDate: 70.00m, Incl(30.00m)));
    }

    // ── Trial balance stays balanced and AR nets down after each payment ─────────────────────────

    [Fact]
    public void After_Merchant_Payment_Trial_Balance_Nets_Zero_And_Ar1200_Nets_Down()
    {
        // Principal 70→100 books AR-Merchant (1200) = 100.00 debit.
        var principal = SettlementPostingTemplates.Principal(Incl(100.00m), Incl(70.00m), Vat)
            .Tag(Partner, Merchant, Period, "ORD-1");

        // Partial 60: 1200 net debit = 100 − 60 = 40 still receivable.
        var pay60 = SettlementPostingTemplates.PaymentReceived(Incl(60.00m), PaymentPayer.Merchant)
            .Tag(Partner, Merchant, Period, "ORD-1");
        var afterPartial = new[] { principal, pay60 };
        SettlementReports.TrialBalanceFor(afterPartial, Period).Net.ShouldBe(0m);
        SettlementReports.Merchant(afterPartial, Merchant, Period).TotalReceivable.Amount.ShouldBe(40.00m);

        // Final 40: 1200 net debit = 0 (fully cleared), trial balance still nets to zero.
        var pay40 = SettlementPostingTemplates.PaymentReceived(Incl(40.00m), PaymentPayer.Merchant)
            .Tag(Partner, Merchant, Period, "ORD-1");
        var afterFull = new[] { principal, pay60, pay40 };
        var trial = SettlementReports.TrialBalanceFor(afterFull, Period);
        trial.IsBalanced.ShouldBeTrue();
        trial.Net.ShouldBe(0m);
        SettlementReports.Merchant(afterFull, Merchant, Period).TotalReceivable.Amount.ShouldBe(0m);
    }

    [Fact]
    public void Payment_Contributes_Nothing_To_Margin_Fee_Or_Net_Vat()
    {
        var principal = SettlementPostingTemplates.Principal(Incl(100.00m), Incl(70.00m), Vat)
            .Tag(Partner, Merchant, Period, "ORD-1");
        var payment = SettlementPostingTemplates.PaymentReceived(Incl(100.00m), PaymentPayer.Merchant)
            .Tag(Partner, Merchant, Period, "ORD-1");

        var withPayment = SettlementReports.Platform(new[] { principal, payment }, Period);
        var withoutPayment = SettlementReports.Platform(new[] { principal }, Period);

        withPayment.ResaleMargin.Amount.ShouldBe(withoutPayment.ResaleMargin.Amount);
        withPayment.FeeRevenue.Amount.ShouldBe(withoutPayment.FeeRevenue.Amount);
        withPayment.NetVatToZatca.Amount.ShouldBe(withoutPayment.NetVatToZatca.Amount);
    }

    [Fact]
    public void Partner_Fee_AR1250_Nets_Down_To_Zero_After_Payment()
    {
        // Chefz partner bulk invoice: fee 5.00 incl billed to the partner books 1250 = 5.00 debit.
        var fee = SettlementPostingTemplates.Fee(Incl(5.00m), ActivationFeePayer.Partner, Vat)
            .Tag(Partner, Merchant, Period, "INV-CHEFZ");
        var payment = SettlementPostingTemplates.PaymentReceived(Incl(5.00m), PaymentPayer.Partner)
            .Tag(Partner, Merchant, Period, "INV-CHEFZ");

        var trial = SettlementReports.TrialBalanceFor(new[] { fee, payment }, Period);
        trial.IsBalanced.ShouldBeTrue();
        trial.Net.ShouldBe(0m);

        // 1250 nets to zero: 5.00 debit (fee) − 5.00 credit (payment).
        var ar1250 = trial.Accounts.Single(a => a.AccountCode == SettlementAccountCode.ArPartner);
        ar1250.Net.ShouldBe(0m);
    }

    // ── Guardrail: nothing live ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Posting_Stays_Off_Nothing_Disbursed()
    {
        var options = new SettlementEngineOptions();
        options.PostingEnabled.ShouldBeFalse();
        options.DisbursementEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Non_Positive_And_Empty_Ref_Payments_Are_Rejected()
    {
        Should.Throw<BusinessException>(() =>
            Payment.Record(Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(0m), DateTime.UtcNow))
            .Code.ShouldBe(SettlementPaymentErrorCodes.NonPositivePayment);

        Should.Throw<BusinessException>(() =>
            Payment.Record(Guid.NewGuid(), "  ", PaymentPayer.Merchant, Merchant, Incl(10m), DateTime.UtcNow))
            .Code.ShouldBe(SettlementPaymentErrorCodes.EmptyAgainstRef);
    }

    // ── Guarded Payment.Record — overpayment + idempotency enforced AT the domain ────────────────

    [Fact]
    public void Guarded_Record_Rejects_Overpayment_120_On_100_Balance()
    {
        var ex = Should.Throw<BusinessException>(() =>
            Payment.Record(Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(120.00m), DateTime.UtcNow,
                arTotal: Incl(100.00m), existingForRef: Array.Empty<Payment>(), idempotencyKey: "RCPT-1"));

        ex.Code.ShouldBe(SettlementPaymentErrorCodes.Overpayment);
    }

    [Fact]
    public void Guarded_Record_Rejects_Overpayment_Across_Multiple_Receipts()
    {
        var ar = Incl(100.00m);
        var first = Payment.Record(Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(70.00m),
            DateTime.UtcNow, arTotal: ar, existingForRef: Array.Empty<Payment>(), idempotencyKey: "RCPT-1");

        // A further 40 against the remaining 30 exceeds the balance and is rejected here, not by the caller.
        var ex = Should.Throw<BusinessException>(() =>
            Payment.Record(Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(40.00m), DateTime.UtcNow,
                arTotal: ar, existingForRef: new[] { first }, idempotencyKey: "RCPT-2"));

        ex.Code.ShouldBe(SettlementPaymentErrorCodes.Overpayment);
    }

    [Fact]
    public void Guarded_Record_Duplicate_Submit_Is_A_NoOp_Returning_The_Same_Receipt()
    {
        var ar = Incl(100.00m);
        var first = Payment.Record(Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(60.00m),
            DateTime.UtcNow, arTotal: ar, existingForRef: Array.Empty<Payment>(), idempotencyKey: "RCPT-1");

        // A re-submit with the SAME key returns the existing receipt — no duplicate, no overpayment throw
        // (dedupe runs BEFORE the guard, so the 60+60 that would breach 100 never gets evaluated).
        var replay = Payment.Record(Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(60.00m),
            DateTime.UtcNow, arTotal: ar, existingForRef: new[] { first }, idempotencyKey: "RCPT-1");

        replay.ShouldBeSameAs(first);
        // The ledger still sees a single 60.00 receipt — money is not double-counted.
        PaymentLedger.PaidToDate("STL-1", new[] { first }).ShouldBe(60.00m);
    }

    [Fact]
    public void Guarded_Record_Partial_60_Then_Partial_40_Reaches_Paid()
    {
        var ar = Incl(100.00m);
        var first = Payment.Record(Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(60.00m),
            DateTime.UtcNow, arTotal: ar, existingForRef: Array.Empty<Payment>(), idempotencyKey: "RCPT-1");
        var second = Payment.Record(Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(40.00m),
            DateTime.UtcNow, arTotal: ar, existingForRef: new[] { first }, idempotencyKey: "RCPT-2");

        second.ShouldNotBeSameAs(first); // a genuine second receipt was recorded (exact remaining accepted)

        var status = PaymentLedger.Status("STL-1", ar, new[] { first, second });
        status.PaidToDate.Amount.ShouldBe(100.00m);
        status.Remaining.Amount.ShouldBe(0m);
        status.State.ShouldBe(PaymentState.Paid);
    }

    [Fact]
    public void Record_Without_Key_Defaults_To_A_Unique_Per_Row_Idempotency_Key()
    {
        var a = Payment.Record(Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(10m), DateTime.UtcNow);
        var b = Payment.Record(Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(10m), DateTime.UtcNow);

        a.IdempotencyKey.ShouldNotBeNullOrWhiteSpace();
        a.IdempotencyKey.ShouldNotBe(b.IdempotencyKey); // legacy single-shot records never collide
    }

    // ── Per-receipt destination + method (FE/BE parity) ──────────────────────────────────────────

    [Fact]
    public void Receipt_Remembers_Its_Bank_Destination_And_Method()
    {
        var receipt = Payment.Record(
            Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(100.00m), DateTime.UtcNow,
            arTotal: Incl(100.00m), existingForRef: Array.Empty<Payment>(),
            method: PaymentMethod.Transfer, bankAccountCode: "1102", idempotencyKey: "RCPT-1");

        receipt.BankAccountCode.ShouldBe("1102");
        receipt.Method.ShouldBe(PaymentMethod.Transfer);
    }

    [Fact]
    public void Stored_Destination_Equals_What_The_Posting_Template_Routes_To()
    {
        // The receipt persists exactly the 110x the cash leg debits — "what posted == what's stored".
        var receipt = Payment.Record(
            Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(50.00m), DateTime.UtcNow,
            method: PaymentMethod.Card, bankAccountCode: "1103");

        var journal = SettlementPostingTemplates.PaymentReceived(receipt.Money, receipt.Payer, receipt.BankAccountCode);
        Line(journal, "1103", EntryDirection.Debit).ShouldBe(50.00m);
        receipt.BankAccountCode.ShouldBe("1103");
    }

    [Fact]
    public void Two_Receipts_On_One_Invoice_To_Different_Banks_Each_Keep_Their_Own_Destination()
    {
        var ar = Incl(100.00m);
        var toBankA = Payment.Record(
            Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(60.00m), DateTime.UtcNow,
            arTotal: ar, existingForRef: Array.Empty<Payment>(),
            method: PaymentMethod.Transfer, bankAccountCode: "1101", idempotencyKey: "RCPT-A");
        var toBankB = Payment.Record(
            Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(40.00m), DateTime.UtcNow,
            arTotal: ar, existingForRef: new[] { toBankA },
            method: PaymentMethod.Card, bankAccountCode: "1102", idempotencyKey: "RCPT-B");

        toBankA.BankAccountCode.ShouldBe("1101");
        toBankB.BankAccountCode.ShouldBe("1102");
        toBankA.Method.ShouldBe(PaymentMethod.Transfer);
        toBankB.Method.ShouldBe(PaymentMethod.Card);
    }

    [Fact]
    public void Each_Method_Enum_Value_Is_Retained()
    {
        foreach (var method in new[]
                 {
                     PaymentMethod.Cash, PaymentMethod.Transfer, PaymentMethod.Card,
                     PaymentMethod.Cod, PaymentMethod.Online, PaymentMethod.Gateway
                 })
        {
            var receipt = Payment.Record(
                Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(1.00m), DateTime.UtcNow,
                method: method);
            receipt.Method.ShouldBe(method);
        }
    }

    [Fact]
    public void No_Bank_Falls_Back_To_1100_Parent_As_Null_Code()
    {
        // Null (or a non-110x code) means the cash leg uses the 1100 parent — stored as null, not "1100".
        var noCode = Payment.Record(Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(10m), DateTime.UtcNow);
        var parentCode = Payment.Record(Guid.NewGuid(), "STL-2", PaymentPayer.Merchant, Merchant, Incl(10m),
            DateTime.UtcNow, method: PaymentMethod.Cash, bankAccountCode: "1100");

        noCode.BankAccountCode.ShouldBeNull();
        parentCode.BankAccountCode.ShouldBeNull(); // 1100 is the parent, not a 110x sub-account

        var journal = SettlementPostingTemplates.PaymentReceived(Incl(10m), PaymentPayer.Merchant, noCode.BankAccountCode);
        Line(journal, SettlementAccountCode.BankCashClearing, EntryDirection.Debit).ShouldBe(10.00m);
    }

    [Fact]
    public void Mixed_Method_Receipts_On_One_Invoice_Accumulate_To_Paid()
    {
        var ar = Incl(100.00m);
        var cash = Payment.Record(
            Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(30.00m), DateTime.UtcNow,
            arTotal: ar, existingForRef: Array.Empty<Payment>(),
            method: PaymentMethod.Cash, bankAccountCode: "1101", idempotencyKey: "RCPT-1");
        var transfer = Payment.Record(
            Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(45.00m), DateTime.UtcNow,
            arTotal: ar, existingForRef: new[] { cash },
            method: PaymentMethod.Transfer, bankAccountCode: "1102", idempotencyKey: "RCPT-2");
        var card = Payment.Record(
            Guid.NewGuid(), "STL-1", PaymentPayer.Merchant, Merchant, Incl(25.00m), DateTime.UtcNow,
            arTotal: ar, existingForRef: new[] { cash, transfer },
            method: PaymentMethod.Card, bankAccountCode: null, idempotencyKey: "RCPT-3");

        var status = PaymentLedger.Status("STL-1", ar, new[] { cash, transfer, card });
        status.PaidToDate.Amount.ShouldBe(100.00m);
        status.State.ShouldBe(PaymentState.Paid);
    }
}
