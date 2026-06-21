using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// A per-partner, per-period verification gate. The engine PROPOSES (ReadyToReconcile / Exception) and
/// never self-confirms; a human accountant COMMITS: a clean match → <see cref="ReconciliationState.Reconciled"/>,
/// an Exception → <see cref="ReconciliationState.ReconciledWithOverride"/> with a MANDATORY audited note.
/// This entity records a state ONLY — it posts no journal and moves no money. The disburse phase Gate-1
/// passes for both committed states (see <see cref="IsReconciled"/>).
/// </summary>
public class ReconciliationBatch : AggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public int PeriodYear { get; private set; }

    public int PeriodMonth { get; private set; }

    public ReconciliationState State { get; private set; }

    public DateTime? ReconciledAt { get; private set; }

    public string? ReconciledBy { get; private set; }

    /// <summary>Loud audit of an override commit (who overrode, mandatory reason). Null otherwise.</summary>
    public string? OverrideReason { get; private set; }

    public string? OverrideBy { get; private set; }

    /// <summary>The reconciled period (derived view over the scalar year/month columns).</summary>
    public SettlementPeriod Period => SettlementPeriod.Of(PeriodYear, PeriodMonth);

    /// <summary>Committed either cleanly or via an audited override — both satisfy disburse Gate-1.</summary>
    public bool IsReconciled =>
        State == ReconciliationState.Reconciled || State == ReconciliationState.ReconciledWithOverride;

    protected ReconciliationBatch()
    {
    }

    private ReconciliationBatch(Guid id, Guid partnerId, SettlementPeriod period)
        : base(id)
    {
        PartnerId = partnerId;
        PeriodYear = period.Year;
        PeriodMonth = period.Month;
        State = ReconciliationState.Open;
    }

    public static ReconciliationBatch Open(Guid id, Guid partnerId, SettlementPeriod period)
    {
        Check.NotNull(period, nameof(period));
        return new ReconciliationBatch(id, partnerId, period);
    }

    /// <summary>
    /// Human COMMIT of a clean match: succeeds (Open → Reconciled) only when the engine proposed
    /// <see cref="ReconciliationProposedState.ReadyToReconcile"/> (funds cover). An Exception is rejected
    /// with <see cref="SettlementReconciliationErrorCodes.ReconcileBlockedFundsNotReceived"/> — it must go
    /// through <see cref="ReconcileWithOverride"/>. Committing an already-committed batch is an idempotent
    /// NO-OP. Moves NO money and posts NO journal.
    /// </summary>
    public void Reconcile(ReconciliationMatch match, string reconciledBy, DateTime at)
    {
        EnsureMatches(match);

        if (IsReconciled)
        {
            return;
        }

        if (match.ProposedState != ReconciliationProposedState.ReadyToReconcile)
        {
            throw new BusinessException(SettlementReconciliationErrorCodes.ReconcileBlockedFundsNotReceived)
                .WithData("PartnerId", PartnerId)
                .WithData("Period", Period.ToString())
                .WithData("FundsReceived", match.FundsReceived.Amount)
                .WithData("CollectedExpected", match.CollectedExpected.Amount);
        }

        State = ReconciliationState.Reconciled;
        ReconciledAt = at;
        ReconciledBy = reconciledBy;
    }

    /// <summary>
    /// Human COMMIT over an Exception with a MANDATORY audited note (Open → ReconciledWithOverride). The
    /// <paramref name="overrideReason"/> is required (else
    /// <see cref="SettlementReconciliationErrorCodes.ReconcileOverrideRequiresNote"/>); who/why/when are
    /// recorded loudly. Committing an already-committed batch is an idempotent NO-OP. Moves NO money and
    /// posts NO journal. This is an accountant action — NOT a disburse action.
    /// </summary>
    public void ReconcileWithOverride(ReconciliationMatch match, string overrideBy, string overrideReason, DateTime at)
    {
        EnsureMatches(match);

        if (string.IsNullOrWhiteSpace(overrideReason))
        {
            throw new BusinessException(SettlementReconciliationErrorCodes.ReconcileOverrideRequiresNote)
                .WithData("PartnerId", PartnerId)
                .WithData("Period", Period.ToString());
        }

        if (IsReconciled)
        {
            return;
        }

        State = ReconciliationState.ReconciledWithOverride;
        ReconciledAt = at;
        ReconciledBy = overrideBy;
        OverrideBy = overrideBy;
        OverrideReason = overrideReason.Trim();
    }

    private void EnsureMatches(ReconciliationMatch match)
    {
        Check.NotNull(match, nameof(match));

        if (match.PartnerId != PartnerId || match.Period != Period)
        {
            throw new BusinessException(SettlementReconciliationErrorCodes.ReconcilePartnerPeriodMismatch)
                .WithData("BatchPartner", PartnerId)
                .WithData("BatchPeriod", Period.ToString())
                .WithData("MatchPartner", match.PartnerId)
                .WithData("MatchPeriod", match.Period.ToString());
        }
    }
}
