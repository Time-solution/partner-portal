using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// RECONCILE only — the verification gate that must pass BEFORE any disbursement. Reconcile proves,
/// per partner per period, that the funds COLLECTED for the period's orders are actually IN (not just
/// invoiced). It records a state ONLY: no journal, no money movement. Anchored to the existing mock
/// numbers — principal 70→100 (payable 70.00, AR 1200 = 100.00) and the Chefz partner fee (AR 1250 = 5.00).
/// </summary>
public class ReconciliationTests
{
    private const decimal Vat = 0.15m;
    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 6);
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly Guid Merchant = Guid.NewGuid();

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static PostingResult PrincipalOrder(string reference) =>
        SettlementPostingTemplates.Principal(Incl(100.00m), Incl(70.00m), Vat)
            .Tag(Partner, Merchant, Period, reference);

    private static PostingResult PartnerFeeOrder(string reference) =>
        SettlementPostingTemplates.Fee(Incl(5.00m), ActivationFeePayer.Partner, Vat)
            .Tag(Partner, Merchant, Period, reference);

    private static Payment MerchantPays(string reference, decimal amount) =>
        Payment.Record(Guid.NewGuid(), reference, PaymentPayer.Merchant, Merchant, Incl(amount), DateTime.UtcNow);

    private static Payment PartnerPays(string reference, decimal amount) =>
        Payment.Record(Guid.NewGuid(), reference, PaymentPayer.Partner, Partner, Incl(amount), DateTime.UtcNow);

    // ── The match ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Match_Reads_Payable_70_And_Collected_Side_100()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };

        var match = Reconciliation.Match(Partner, Period, entries, Array.Empty<Payment>());

        match.AmountOwedToPartner.Amount.ShouldBe(70.00m);   // net 2100
        match.CollectedExpected.Amount.ShouldBe(100.00m);    // AR booked (1200)
        match.FundsReceived.Amount.ShouldBe(0m);
        match.FundsCovered.ShouldBeFalse();
    }

    // ── Blocked / short / covered ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Before_Any_Payment_Reconcile_Is_Blocked_Funds_Not_Received()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };
        var match = Reconciliation.Match(Partner, Period, entries, Array.Empty<Payment>());

        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);

        var ex = Should.Throw<BusinessException>(() => batch.Reconcile(match, "accountant", DateTime.UtcNow));
        ex.Code.ShouldBe(SettlementReconciliationErrorCodes.ReconcileBlockedFundsNotReceived);

        batch.State.ShouldBe(ReconciliationState.Open);
        Reconciliation.Status(match, batch).BlockedReason.ShouldBe(SettlementReconciliationConsts.BlockedFundsNotReceived);
    }

    [Fact]
    public void Full_Payment_Of_100_Lets_Reconcile_Succeed()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };
        var payments = new[] { MerchantPays("ORD-1", 100.00m) };
        var match = Reconciliation.Match(Partner, Period, entries, payments);

        match.FundsReceived.Amount.ShouldBe(100.00m);
        match.FundsCovered.ShouldBeTrue();

        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        batch.Reconcile(match, "accountant", DateTime.UtcNow);

        batch.State.ShouldBe(ReconciliationState.Reconciled);
        batch.ReconciledBy.ShouldBe("accountant");
        batch.ReconciledAt.ShouldNotBeNull();

        var status = Reconciliation.Status(match, batch);
        status.IsReconciled.ShouldBeTrue();
        status.BlockedReason.ShouldBeNull();
    }

    [Fact]
    public void Partial_Payment_60_Of_100_Reconcile_Is_Blocked_Short()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };
        var payments = new[] { MerchantPays("ORD-1", 60.00m) };
        var match = Reconciliation.Match(Partner, Period, entries, payments);

        match.FundsReceived.Amount.ShouldBe(60.00m);
        match.FundsCovered.ShouldBeFalse();

        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        Should.Throw<BusinessException>(() => batch.Reconcile(match, "accountant", DateTime.UtcNow))
            .Code.ShouldBe(SettlementReconciliationErrorCodes.ReconcileBlockedFundsNotReceived);
        batch.State.ShouldBe(ReconciliationState.Open);
    }

    [Fact]
    public void Chefz_Partner_Fee_Reconciles_Only_After_The_5_Is_Received()
    {
        var entries = new[] { PartnerFeeOrder("INV-CHEFZ") };

        // Before payment: collected side = 5.00 (AR 1250), funds 0 → blocked.
        var dry = Reconciliation.Match(Partner, Period, entries, Array.Empty<Payment>());
        dry.CollectedExpected.Amount.ShouldBe(5.00m);
        dry.FundsCovered.ShouldBeFalse();

        // After the partner pays 5.00 → covered, reconcile succeeds.
        var payments = new[] { PartnerPays("INV-CHEFZ", 5.00m) };
        var match = Reconciliation.Match(Partner, Period, entries, payments);
        match.FundsReceived.Amount.ShouldBe(5.00m);
        match.FundsCovered.ShouldBeTrue();

        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        batch.Reconcile(match, "platform-admin", DateTime.UtcNow);
        batch.State.ShouldBe(ReconciliationState.Reconciled);
    }

    // ── No journal / no money movement ────────────────────────────────────────────────────────────

    [Fact]
    public void Reconcile_Posts_No_Journal_And_Leaves_Trial_Balance_Unchanged()
    {
        var order = PrincipalOrder("ORD-1");
        var paymentJournal = SettlementPostingTemplates.PaymentReceived(Incl(100.00m), PaymentPayer.Merchant)
            .Tag(Partner, Merchant, Period, "ORD-1");
        var journals = new[] { order, paymentJournal };

        var before = SettlementReports.TrialBalanceFor(journals, Period);

        var match = Reconciliation.Match(Partner, Period, new[] { order }, new[] { MerchantPays("ORD-1", 100.00m) });
        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        batch.Reconcile(match, "accountant", DateTime.UtcNow);

        // Reconcile adds no journal: the same journal set still nets to zero, identical totals.
        var after = SettlementReports.TrialBalanceFor(journals, Period);
        after.IsBalanced.ShouldBeTrue();
        after.Net.ShouldBe(0m);
        after.TotalDebits.Amount.ShouldBe(before.TotalDebits.Amount);
        after.TotalCredits.Amount.ShouldBe(before.TotalCredits.Amount);
    }

    // ── Idempotent ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Reconciling_An_Already_Reconciled_Batch_Is_A_NoOp()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };
        var match = Reconciliation.Match(Partner, Period, entries, new[] { MerchantPays("ORD-1", 100.00m) });

        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        batch.Reconcile(match, "accountant", new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc));
        var firstReconciledAt = batch.ReconciledAt;

        // Second reconcile is a no-op — no throw, state and audit stamp unchanged.
        Should.NotThrow(() => batch.Reconcile(match, "someone-else", DateTime.UtcNow));
        batch.State.ShouldBe(ReconciliationState.Reconciled);
        batch.ReconciledBy.ShouldBe("accountant");
        batch.ReconciledAt.ShouldBe(firstReconciledAt);
    }

    [Fact]
    public void Match_For_A_Different_Partner_Period_Is_Rejected()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };
        var match = Reconciliation.Match(Partner, Period, entries, new[] { MerchantPays("ORD-1", 100.00m) });

        var otherPeriodBatch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, SettlementPeriod.Of(2026, 7));
        Should.Throw<BusinessException>(() => otherPeriodBatch.Reconcile(match, "accountant", DateTime.UtcNow))
            .Code.ShouldBe(SettlementReconciliationErrorCodes.ReconcilePartnerPeriodMismatch);
    }

    // ── Human-committed flow: engine PROPOSES, accountant COMMITS ─────────────────────────────────

    [Fact]
    public void Funds_Cover_Proposes_ReadyToReconcile_But_Does_Not_Auto_Commit()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };
        var match = Reconciliation.Match(Partner, Period, entries, new[] { MerchantPays("ORD-1", 100.00m) });

        // Engine proposes Ready — but never self-confirms: the batch stays Open until a human commits.
        match.ProposedState.ShouldBe(ReconciliationProposedState.ReadyToReconcile);

        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        var status = Reconciliation.Status(match, batch);

        status.ProposedState.ShouldBe(ReconciliationProposedState.ReadyToReconcile);
        status.CommittedState.ShouldBe(ReconciliationState.Open);
        status.IsReconciled.ShouldBeFalse();
        batch.State.ShouldBe(ReconciliationState.Open);
    }

    [Fact]
    public void Accountant_Commits_A_ReadyToReconcile_To_Reconciled()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };
        var match = Reconciliation.Match(Partner, Period, entries, new[] { MerchantPays("ORD-1", 100.00m) });

        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        batch.Reconcile(match, "accountant", DateTime.UtcNow);

        batch.State.ShouldBe(ReconciliationState.Reconciled);
        var status = Reconciliation.Status(match, batch);
        status.CommittedState.ShouldBe(ReconciliationState.Reconciled);
        status.IsReconciled.ShouldBeTrue();
        status.OverrideReason.ShouldBeNull();
        status.OverrideBy.ShouldBeNull();
    }

    // ── Exception + override-with-note ────────────────────────────────────────────────────────────

    [Fact]
    public void Short_Funds_Propose_Exception()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };
        var match = Reconciliation.Match(Partner, Period, entries, new[] { MerchantPays("ORD-1", 60.00m) });

        match.ProposedState.ShouldBe(ReconciliationProposedState.Exception);
        Reconciliation.Status(match, null).ProposedState.ShouldBe(ReconciliationProposedState.Exception);
    }

    [Fact]
    public void Committing_An_Exception_Without_A_Note_Is_Rejected()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };
        var match = Reconciliation.Match(Partner, Period, entries, new[] { MerchantPays("ORD-1", 60.00m) });

        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);

        // Plain commit refuses the Exception (funds short) — it must go through an audited override.
        Should.Throw<BusinessException>(() => batch.Reconcile(match, "accountant", DateTime.UtcNow))
            .Code.ShouldBe(SettlementReconciliationErrorCodes.ReconcileBlockedFundsNotReceived);

        // Override WITHOUT a note is also rejected — the note is mandatory.
        Should.Throw<BusinessException>(() => batch.ReconcileWithOverride(match, "accountant", "  ", DateTime.UtcNow))
            .Code.ShouldBe(SettlementReconciliationErrorCodes.ReconcileOverrideRequiresNote);

        batch.State.ShouldBe(ReconciliationState.Open);
    }

    [Fact]
    public void Committing_An_Exception_With_A_Note_Records_An_Audited_Override()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };
        var match = Reconciliation.Match(Partner, Period, entries, new[] { MerchantPays("ORD-1", 60.00m) });

        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        batch.ReconcileWithOverride(match, "accountant", "Partner confirmed wire in transit (ref WX-9981)", DateTime.UtcNow);

        batch.State.ShouldBe(ReconciliationState.ReconciledWithOverride);
        batch.OverrideBy.ShouldBe("accountant");
        batch.OverrideReason.ShouldBe("Partner confirmed wire in transit (ref WX-9981)");
        batch.IsReconciled.ShouldBeTrue();

        var status = Reconciliation.Status(match, batch);
        status.CommittedState.ShouldBe(ReconciliationState.ReconciledWithOverride);
        status.ProposedState.ShouldBe(ReconciliationProposedState.Exception);
        status.OverrideBy.ShouldBe("accountant");
        status.OverrideReason.ShouldNotBeNullOrWhiteSpace();
        status.IsReconciled.ShouldBeTrue();
    }

    [Fact]
    public void Overriding_An_Already_Committed_Batch_Is_A_NoOp()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };
        var match = Reconciliation.Match(Partner, Period, entries, new[] { MerchantPays("ORD-1", 60.00m) });

        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        batch.ReconcileWithOverride(match, "accountant", "first reason", new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc));
        var firstAt = batch.ReconciledAt;

        Should.NotThrow(() => batch.ReconcileWithOverride(match, "someone-else", "second reason", DateTime.UtcNow));
        batch.OverrideBy.ShouldBe("accountant");
        batch.OverrideReason.ShouldBe("first reason");
        batch.ReconciledAt.ShouldBe(firstAt);
    }

    // ── Grouped + partial group reconcile ─────────────────────────────────────────────────────────

    [Fact]
    public void Group_Of_Three_Reconciles_The_Two_Ready_And_Leaves_The_Short_As_Exception()
    {
        // A merchant's three partners for one period: 2 ready (funds in), 1 short.
        var members = new[]
        {
            BuildMember(amountReceived: 100.00m),  // ready
            BuildMember(amountReceived: 100.00m),  // ready
            BuildMember(amountReceived: 40.00m)    // short → exception
        };

        var view = GroupReconciliation.View(members);
        view.ReadyCount.ShouldBe(2);
        view.ExceptionCount.ShouldBe(1);
        view.CommittedCount.ShouldBe(0);

        var results = GroupReconciliation.ReconcileGroup(members, "accountant", DateTime.UtcNow);

        results.Count(r => r.Outcome == GroupReconcileOutcome.Reconciled).ShouldBe(2);
        results.Count(r => r.Outcome == GroupReconcileOutcome.LeftAsException).ShouldBe(1);

        // Partial group reconcile: the clean ones commit; the short one stays Open (not blocked, not overridden).
        members[0].Batch.State.ShouldBe(ReconciliationState.Reconciled);
        members[1].Batch.State.ShouldBe(ReconciliationState.Reconciled);
        members[2].Batch.State.ShouldBe(ReconciliationState.Open);

        var after = GroupReconciliation.View(members);
        after.CommittedCount.ShouldBe(2);
        after.ExceptionCount.ShouldBe(1);
    }

    // ── Both committed states satisfy disburse Gate-1 ─────────────────────────────────────────────

    [Fact]
    public void Reconciled_And_ReconciledWithOverride_Both_Pass_Disburse_Gate1()
    {
        var entries = new[] { PrincipalOrder("ORD-1") };

        // Clean commit.
        var cleanMatch = Reconciliation.Match(Partner, Period, entries, new[] { MerchantPays("ORD-1", 100.00m) });
        var cleanBatch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        cleanBatch.Reconcile(cleanMatch, "accountant", DateTime.UtcNow);
        DisbursementContext.From(cleanMatch, cleanBatch).IsReconciled.ShouldBeTrue();

        // Override commit — Gate-1 must also pass (overridden funds are now the human-accepted truth).
        var shortMatch = Reconciliation.Match(Partner, Period, entries, new[] { MerchantPays("ORD-1", 60.00m) });
        var overrideBatch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        overrideBatch.ReconcileWithOverride(shortMatch, "accountant", "wire confirmed", DateTime.UtcNow);
        DisbursementContext.From(shortMatch, overrideBatch).IsReconciled.ShouldBeTrue();
    }

    [Fact]
    public void Override_Posts_No_Journal_And_Leaves_Trial_Balance_Unchanged()
    {
        var order = PrincipalOrder("ORD-1");
        var paymentJournal = SettlementPostingTemplates.PaymentReceived(Incl(60.00m), PaymentPayer.Merchant)
            .Tag(Partner, Merchant, Period, "ORD-1");
        var journals = new[] { order, paymentJournal };

        var before = SettlementReports.TrialBalanceFor(journals, Period);

        var match = Reconciliation.Match(Partner, Period, new[] { order }, new[] { MerchantPays("ORD-1", 60.00m) });
        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        batch.ReconcileWithOverride(match, "accountant", "wire confirmed", DateTime.UtcNow);

        var after = SettlementReports.TrialBalanceFor(journals, Period);
        after.IsBalanced.ShouldBeTrue();
        after.TotalDebits.Amount.ShouldBe(before.TotalDebits.Amount);
        after.TotalCredits.Amount.ShouldBe(before.TotalCredits.Amount);
    }

    private static ReconciliationGroupMember BuildMember(decimal amountReceived)
    {
        var partner = Guid.NewGuid();
        var reference = "ORD-" + partner.ToString("N")[..8];

        var entries = new[]
        {
            SettlementPostingTemplates.Principal(Incl(100.00m), Incl(70.00m), Vat)
                .Tag(partner, Merchant, Period, reference)
        };
        var payments = new[]
        {
            Payment.Record(Guid.NewGuid(), reference, PaymentPayer.Merchant, Merchant, Incl(amountReceived), DateTime.UtcNow)
        };

        var match = Reconciliation.Match(partner, Period, entries, payments);
        var batch = ReconciliationBatch.Open(Guid.NewGuid(), partner, Period);
        return new ReconciliationGroupMember(partner, batch, match);
    }

    // ── Guardrail: nothing live ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Disbursement_And_Posting_Stay_Off()
    {
        var options = new SettlementEngineOptions();
        options.DisbursementEnabled.ShouldBeFalse();
        options.PostingEnabled.ShouldBeFalse();
    }
}
