using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// One outbound payout row (money OUT to the partner) for a partner+period payable. APPEND-ONLY:
/// several disbursements may target one payable (partial allowed) and corrections are NEW reversal
/// rows (<see cref="ReversalOf"/> set), never edits.
///
/// Created LOCKED by default. Even when the three system gates pass (reconciled / within funds
/// received / within payable), the row posts and releases NOTHING until an authorized human with the
/// Disburse privilege explicitly <see cref="Release"/>s it — separation of duties from the accountant
/// who reconciles. A Locked row computes no journal even if <c>DisbursementEnabled</c> were ON.
/// </summary>
public class Disbursement : AggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public int PeriodYear { get; private set; }

    public int PeriodMonth { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = SettlementConsts.DefaultCurrency;

    public DateTime Date { get; private set; }

    /// <summary>Double-submit safety: the same key resolves to ONE row (unique in persistence).</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    public DisbursementState State { get; private set; }

    public string? ReleasedBy { get; private set; }

    public DateTime? ReleasedAt { get; private set; }

    /// <summary>When set, this row reverses the referenced disbursement (append-only correction).</summary>
    public Guid? ReversalOf { get; private set; }

    public SettlementPeriod Period => SettlementPeriod.Of(PeriodYear, PeriodMonth);

    public Money Money => Money.Of(Amount, Currency, vatInclusive: true);

    public bool IsReversal => ReversalOf.HasValue;

    public bool IsReleased => State == DisbursementState.Released;

    protected Disbursement()
    {
    }

    private Disbursement(
        Guid id,
        Guid partnerId,
        SettlementPeriod period,
        Money amount,
        string idempotencyKey,
        DateTime date,
        DisbursementState state,
        Guid? reversalOf)
        : base(id)
    {
        PartnerId = partnerId;
        PeriodYear = period.Year;
        PeriodMonth = period.Month;
        Amount = amount.Amount;
        Currency = amount.Currency;
        IdempotencyKey = idempotencyKey;
        Date = date;
        State = state;
        ReversalOf = reversalOf;
    }

    /// <summary>
    /// Creates a payout row in the default LOCKED state. The three system gates must already have
    /// passed (see <see cref="Disbursements"/>); this records intent only and posts nothing.
    /// </summary>
    public static Disbursement CreateLocked(
        Guid id,
        Guid partnerId,
        SettlementPeriod period,
        Money amount,
        string idempotencyKey,
        DateTime date)
    {
        Check.NotNull(period, nameof(period));
        Check.NotNull(amount, nameof(amount));

        if (!amount.IsPositive)
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.NonPositiveDisbursement)
                .WithData("Amount", amount.Amount);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.EmptyIdempotencyKey);
        }

        return new Disbursement(id, partnerId, period, amount, idempotencyKey.Trim(), date,
            DisbursementState.Locked, reversalOf: null);
    }

    /// <summary>
    /// Creates an append-only REVERSAL of <paramref name="original"/> for the SAME (full) amount. Gated:
    /// the actor MUST hold the Disburse privilege (<paramref name="actorHasDisbursePrivilege"/>) and the
    /// original MUST be <see cref="DisbursementState.Released"/> — you cannot reverse a payout that never
    /// actually went out (Locked) or a reversal row. A reversal is itself considered Released (its inverse
    /// journal Dr 1100 / Cr 2100 nets the original back).
    ///
    /// PARTIAL-REVERSAL LIMITATION (by design, for now): only a FULL-amount reversal is supported. A safe
    /// partial reversal would have to cap the CUMULATIVE reversed amount across multiple partial rows at
    /// the original payout (a running-total invariant), which is a coordinator-level concern — exactly like
    /// <see cref="Disbursements.CreateOrGet"/> caps cumulative disbursement against the payable. Until that
    /// coordinator cap exists, exposing a per-row partial amount here would allow over-reversal across
    /// replays, so it is intentionally NOT offered. To partially unwind today, reverse in full and re-issue
    /// a smaller disbursement through <see cref="Disbursements.CreateOrGet"/>.
    /// </summary>
    public static Disbursement CreateReversal(
        Guid id,
        Disbursement original,
        string idempotencyKey,
        bool actorHasDisbursePrivilege,
        DateTime date)
    {
        Check.NotNull(original, nameof(original));

        // Disburse privilege is required to reverse (same separation of duties as releasing a payout).
        if (!actorHasDisbursePrivilege)
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.DisburseBlockedNotAuthorized)
                .WithData("ReversalOf", original.Id);
        }

        if (original.Id == Guid.Empty)
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.InvalidReversalLink);
        }

        // Only a payout that actually RELEASED can be reversed — never a Locked (un-paid) or a reversal row.
        if (original.State != DisbursementState.Released)
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.DisburseReversalRequiresReleased)
                .WithData("ReversalOf", original.Id)
                .WithData("State", original.State.ToString());
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.EmptyIdempotencyKey);
        }

        return new Disbursement(id, original.PartnerId, original.Period, original.Money, idempotencyKey.Trim(),
            date, DisbursementState.Released, reversalOf: original.Id);
    }

    /// <summary>
    /// Releases the human lock. Two human gates, enforced HERE (not just by which permissions a role holds):
    ///   (1) the actor MUST hold the Disburse privilege (<paramref name="actorHasDisbursePrivilege"/>) — the
    ///       reconciling accountant, who lacks it, is rejected with
    ///       <see cref="SettlementDisbursementErrorCodes.DisburseBlockedNotAuthorized"/>; and
    ///   (2) the TWO-PERSON rule — the releaser MUST NOT be the same human who reconciled the batch
    ///       (<paramref name="reconciledBy"/>). Even a PlatformAdmin holding BOTH privileges is blocked from
    ///       doing both gates on the SAME item, with
    ///       <see cref="SettlementDisbursementErrorCodes.DisburseBlockedSameActorAsReconciler"/> (compared by
    ///       actor id, case-insensitive — not by permission).
    /// Records the who/when audit. Releasing an already-Released row is an idempotent NO-OP.
    /// </summary>
    public void Release(string releasedBy, bool actorHasDisbursePrivilege, string reconciledBy, DateTime at)
    {
        if (!actorHasDisbursePrivilege)
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.DisburseBlockedNotAuthorized)
                .WithData("ReleasedBy", releasedBy);
        }

        if (State == DisbursementState.Released)
        {
            return;
        }

        // Two-person rule: a single human cannot both reconcile AND release the same payout.
        if (IsSameActor(releasedBy, reconciledBy))
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.DisburseBlockedSameActorAsReconciler)
                .WithData("Actor", releasedBy?.Trim())
                .WithData("ReconciledBy", reconciledBy?.Trim());
        }

        State = DisbursementState.Released;
        ReleasedBy = releasedBy;
        ReleasedAt = at;
    }

    private static bool IsSameActor(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The journal for this row. A LOCKED row computes NOTHING (a non-posting result) regardless of any
    /// flag. A RELEASED row computes Dr 2100 / Cr 1100 (or the inverse for a reversal) — COMPUTE ONLY;
    /// nothing posts live while <c>DisbursementEnabled</c> is OFF.
    /// </summary>
    public PostingResult ComputeJournal()
    {
        if (State != DisbursementState.Released)
        {
            return PostingResult.NonPosting(ParticipationMode.Disbursement, Currency);
        }

        var journal = IsReversal
            ? SettlementPostingTemplates.DisbursementReversal(Money)
            : SettlementPostingTemplates.Disbursement(Money);

        // A payout is partner-scoped; there is no merchant dimension on the money-out leg.
        return journal.Tag(PartnerId, Guid.Empty, Period, IdempotencyKey);
    }
}
