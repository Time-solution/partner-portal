using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// DISBURSE — money OUT to the partner, gated on the Reconciled state. Three system gates (reconciled
/// / capped by funds received / not over payable) PLUS a human release lock (default Locked, released
/// only by the disburse role). Anchored to the mock numbers: principal 70→100 → payable 70.00, AR
/// 1200 = 100.00. DisbursementEnabled stays OFF: a Released, gate-passed payout only COMPUTES the
/// journal — nothing posts live, and a Locked payout computes nothing at all.
/// </summary>
public class DisbursementTests
{
    private const decimal Vat = 0.15m;
    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 6);
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly Guid Merchant = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static decimal Line(PostingResult r, string code, EntryDirection dir) =>
        r.LineFor(code, dir).ShouldNotBeNull().Amount.Amount;

    private static PostingResult Order(string reference) =>
        SettlementPostingTemplates.Principal(Incl(100.00m), Incl(70.00m), Vat).Tag(Partner, Merchant, Period, reference);

    private static Payment MerchantPays(string reference, decimal amount) =>
        Payment.Record(Guid.NewGuid(), reference, PaymentPayer.Merchant, Merchant, Incl(amount), DateTime.UtcNow);

    /// <summary>A disburse context built through the reconcile layer: reconciled iff the batch is reconciled.</summary>
    private static DisbursementContext Context(decimal merchantPaid, bool reconciled)
    {
        var entries = new[] { Order("ORD-1") };
        var payments = merchantPaid > 0 ? new[] { MerchantPays("ORD-1", merchantPaid) } : Array.Empty<Payment>();
        var match = Reconciliation.Match(Partner, Period, entries, payments);

        ReconciliationBatch? batch = null;
        if (reconciled)
        {
            batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
            batch.Reconcile(match, "accountant", At);
        }

        return DisbursementContext.From(match, batch);
    }

    // ── Gate 1: reconciled ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Not_Reconciled_Disburse_Is_Blocked()
    {
        var ctx = Context(merchantPaid: 100m, reconciled: false);

        var ex = Should.Throw<BusinessException>(() =>
            Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, Array.Empty<Disbursement>()));
        ex.Code.ShouldBe(SettlementDisbursementErrorCodes.DisburseBlockedNotReconciled);
    }

    // ── Default LOCKED, computes nothing until released ───────────────────────────────────────────

    [Fact]
    public void Reconciled_Create_70_Is_Locked_And_Posts_Nothing()
    {
        var ctx = Context(merchantPaid: 100m, reconciled: true);

        var disb = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, Array.Empty<Disbursement>());

        disb.State.ShouldBe(DisbursementState.Locked);
        disb.ReleasedBy.ShouldBeNull();

        // A Locked row computes NO journal (no lines) — nothing posted.
        disb.ComputeJournal().IsFinancial.ShouldBeFalse();
    }

    [Fact]
    public void Release_By_Disburse_Role_Computes_Dr2100_Cr1100_And_Remaining_Zero()
    {
        var ctx = Context(merchantPaid: 100m, reconciled: true);
        var disb = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, Array.Empty<Disbursement>());

        disb.Release("platform-admin", actorHasDisbursePrivilege: true, At);

        disb.State.ShouldBe(DisbursementState.Released);
        var journal = disb.ComputeJournal();
        Line(journal, SettlementAccountCode.ApPartner, EntryDirection.Debit).ShouldBe(70.00m);
        Line(journal, SettlementAccountCode.BankCashClearing, EntryDirection.Credit).ShouldBe(70.00m);
        journal.IsBalanced.ShouldBeTrue();

        var status = Disbursements.Status(Partner, Period, ctx, new[] { disb });
        status.RemainingToDisburse.Amount.ShouldBe(0m);
        status.State.ShouldBe(DisbursementPositionState.FullyDisbursed);
    }

    // ── Partial / multiple ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Partial_40_Then_30_Walks_Remaining_30_Then_0()
    {
        var ctx = Context(merchantPaid: 100m, reconciled: true);

        var first = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(40m), "DISB-1", At, ctx, Array.Empty<Disbursement>());
        Disbursements.Status(Partner, Period, ctx, new[] { first }).RemainingToDisburse.Amount.ShouldBe(30.00m);
        Disbursements.Status(Partner, Period, ctx, new[] { first }).State.ShouldBe(DisbursementPositionState.PartiallyDisbursed);

        var second = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(30m), "DISB-2", At, ctx, new[] { first });
        Disbursements.Status(Partner, Period, ctx, new[] { first, second }).RemainingToDisburse.Amount.ShouldBe(0m);
    }

    // ── Gate 3: over payable ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Disburse_80_On_70_Payable_Is_Blocked_ExceedsPayable()
    {
        var ctx = Context(merchantPaid: 100m, reconciled: true);

        Should.Throw<BusinessException>(() =>
            Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(80m), "DISB-1", At, ctx, Array.Empty<Disbursement>()))
            .Code.ShouldBe(SettlementDisbursementErrorCodes.DisburseBlockedExceedsPayable);
    }

    // ── Gate 2: capped by received funds ──────────────────────────────────────────────────────────

    [Fact]
    public void Reconciled_But_Funds_Only_Partially_In_Caps_At_Funds_Received()
    {
        // Reconcile would itself block on short funds, but assert the disburse gate independently:
        // payable 70, only 60 received → paying 70 exceeds funds received.
        var ctx = new DisbursementContext(Incl(70m), Incl(60m), IsReconciled: true);

        Should.Throw<BusinessException>(() =>
            Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, Array.Empty<Disbursement>()))
            .Code.ShouldBe(SettlementDisbursementErrorCodes.DisburseBlockedExceedsFundsReceived);

        // Up to the 60 actually in is fine.
        var ok = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(60m), "DISB-2", At, ctx, Array.Empty<Disbursement>());
        ok.State.ShouldBe(DisbursementState.Locked);
    }

    // ── Human lock: gates pass but NOT released → posts nothing ───────────────────────────────────

    [Fact]
    public void All_Gates_Pass_But_Not_Released_Posts_Nothing()
    {
        var ctx = Context(merchantPaid: 100m, reconciled: true);
        var disb = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, Array.Empty<Disbursement>());

        // Still Locked → computes no journal even though all three gates passed.
        disb.State.ShouldBe(DisbursementState.Locked);
        disb.ComputeJournal().Lines.Count.ShouldBe(0);
    }

    // ── Separation of duties: accountant cannot release ───────────────────────────────────────────

    [Fact]
    public void Accountant_Attempting_Release_Is_Blocked()
    {
        var ctx = Context(merchantPaid: 100m, reconciled: true);
        var disb = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, Array.Empty<Disbursement>());

        // The accountant lacks the Disburse privilege.
        Should.Throw<BusinessException>(() => disb.Release("accountant", actorHasDisbursePrivilege: false, At))
            .Code.ShouldBe(SettlementDisbursementErrorCodes.DisburseBlockedNotAuthorized);

        disb.State.ShouldBe(DisbursementState.Locked);
    }

    // ── Release audit + idempotent re-release ─────────────────────────────────────────────────────

    [Fact]
    public void Release_Is_Logged_And_Re_Release_Is_A_NoOp()
    {
        var ctx = Context(merchantPaid: 100m, reconciled: true);
        var disb = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, Array.Empty<Disbursement>());

        disb.Release("platform-admin", true, At);
        disb.ReleasedBy.ShouldBe("platform-admin");
        disb.ReleasedAt.ShouldBe(At);

        Should.NotThrow(() => disb.Release("someone-else", true, At.AddDays(1)));
        disb.ReleasedBy.ShouldBe("platform-admin");
        disb.ReleasedAt.ShouldBe(At);
    }

    // ── Idempotency: same key twice → one row ─────────────────────────────────────────────────────

    [Fact]
    public void Same_Idempotency_Key_Twice_Yields_One_Disbursement()
    {
        var ctx = Context(merchantPaid: 100m, reconciled: true);

        var first = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, Array.Empty<Disbursement>());
        var again = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, new[] { first });

        again.Id.ShouldBe(first.Id);
        Disbursements.NetDisbursed(Partner, Period, new[] { first }).ShouldBe(70.00m);
    }

    // ── Reversal nets the payout back; trial balance still zero ───────────────────────────────────

    [Fact]
    public void Reversal_Nets_The_Disbursement_Back_Trial_Balance_Zero()
    {
        var ctx = Context(merchantPaid: 100m, reconciled: true);
        var disb = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, Array.Empty<Disbursement>());
        disb.Release("platform-admin", true, At);

        var reversal = Disbursement.CreateReversal(Guid.NewGuid(), disb, "DISB-1-REV", At.AddDays(1));
        reversal.IsReversal.ShouldBeTrue();
        reversal.ReversalOf.ShouldBe(disb.Id);

        var journals = new[] { disb.ComputeJournal(), reversal.ComputeJournal() };
        var trial = SettlementReports.TrialBalanceFor(journals, Period);
        trial.IsBalanced.ShouldBeTrue();
        trial.Net.ShouldBe(0m);

        // Net disbursed returns to zero after the reversal row.
        Disbursements.NetDisbursed(Partner, Period, new[] { disb, reversal }).ShouldBe(0m);
    }

    // ── Whole money cycle: after disbursement, trial balance nets zero ────────────────────────────

    [Fact]
    public void Full_Cycle_Order_Payment_Disbursement_Nets_To_Zero()
    {
        var order = Order("ORD-1");
        var payment = SettlementPostingTemplates.PaymentReceived(Incl(100m), PaymentPayer.Merchant)
            .Tag(Partner, Merchant, Period, "ORD-1");

        var ctx = Context(merchantPaid: 100m, reconciled: true);
        var disb = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, Array.Empty<Disbursement>());
        disb.Release("platform-admin", true, At);

        var trial = SettlementReports.TrialBalanceFor(new[] { order, payment, disb.ComputeJournal() }, Period);
        trial.IsBalanced.ShouldBeTrue();
        trial.Net.ShouldBe(0m);

        // Partner payable (net 2100) nets down by the 70 paid out: 70 booked − 70 disbursed = 0.
        SettlementReports.Partner(new[] { order, payment, disb.ComputeJournal() }, Partner, Period)
            .TotalPayable.Amount.ShouldBe(0m);
    }

    // ── Guardrail: nothing live ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Disbursement_Flag_Stays_Off()
    {
        new SettlementEngineOptions().DisbursementEnabled.ShouldBeFalse();
        new SettlementEngineOptions().PostingEnabled.ShouldBeFalse();
    }
}
