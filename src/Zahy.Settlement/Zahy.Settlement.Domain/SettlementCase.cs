using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// One settlement followed through the lifecycle, scoped to a single <see cref="SettlementBook"/>
/// and partner. Idempotency is keyed by (Book, ExternalTransactionId): replaying the same external
/// event resolves to the same case via <see cref="IdempotencyKey"/> (a unique index in persistence,
/// added in the next Phase-2 increment). State only advances one legal step at a time and every
/// transition is recorded append-only.
/// </summary>
public class SettlementCase : AggregateRoot<Guid>
{
    private readonly List<SettlementStateTransition> _history = new();

    public SettlementBook Book { get; private set; }

    public Guid PartnerId { get; private set; }

    public string ExternalTransactionId { get; private set; } = string.Empty;

    public SettlementCaseState State { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>When set, this case reverses the referenced original (append-only correction trail).</summary>
    public Guid? ReversesSettlementCaseId { get; private set; }

    /// <summary>Operator-supplied justification for a reversal case (null on ordinary cases).</summary>
    public string? Reason { get; private set; }

    /// <summary>The user who triggered a reversal (null on ordinary cases).</summary>
    public Guid? ReversedByUserId { get; private set; }

    public IReadOnlyList<SettlementStateTransition> History => new ReadOnlyCollection<SettlementStateTransition>(_history);

    /// <summary>Stable dedupe key. Trimmed + lower-cased so casing/whitespace can't create duplicates.</summary>
    public string IdempotencyKey => BuildIdempotencyKey(Book, ExternalTransactionId);

    protected SettlementCase()
    {
    }

    private SettlementCase(Guid id, SettlementBook book, Guid partnerId, string externalTransactionId, DateTime createdAt)
        : base(id)
    {
        Book = book;
        PartnerId = partnerId;
        ExternalTransactionId = externalTransactionId;
        State = SettlementCaseState.Collected;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        _history.Add(new SettlementStateTransition(null, SettlementCaseState.Collected, createdAt));
    }

    public static SettlementCase Start(
        Guid id,
        SettlementBook book,
        Guid partnerId,
        string externalTransactionId,
        DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(externalTransactionId))
        {
            throw new BusinessException(SettlementCaseErrorCodes.EmptyExternalTransactionId);
        }

        return new SettlementCase(id, book, partnerId, externalTransactionId.Trim(), createdAt);
    }

    public void TransitionTo(SettlementCaseState target, DateTime at)
    {
        if (!SettlementStateMachine.CanTransition(State, target))
        {
            throw new BusinessException(SettlementCaseErrorCodes.IllegalStateTransition)
                .WithData("From", State.ToString())
                .WithData("To", target.ToString());
        }

        _history.Add(new SettlementStateTransition(State, target, at));
        State = target;
        UpdatedAt = at;
    }

    /// <summary>Advance to the next legal state. Throws if already terminal (Reconciled).</summary>
    public void Advance(DateTime at)
    {
        var next = SettlementStateMachine.NextOf(State);
        if (next == null)
        {
            throw new BusinessException(SettlementCaseErrorCodes.IllegalStateTransition)
                .WithData("From", State.ToString())
                .WithData("To", "<none>");
        }

        TransitionTo(next.Value, at);
    }

    public void LinkReversal(Guid originalSettlementCaseId)
    {
        if (originalSettlementCaseId == Guid.Empty)
        {
            throw new BusinessException(SettlementCaseErrorCodes.InvalidReversalLink);
        }

        ReversesSettlementCaseId = originalSettlementCaseId;
    }

    /// <summary>
    /// States from which a settlement case may be reversed by an append-only compensating case.
    /// A freshly Collected case has nothing posted yet, and a Reconciled case is terminal — neither
    /// is reversible through this path.
    /// </summary>
    public static bool IsReversibleState(SettlementCaseState state) =>
        state is SettlementCaseState.Allocated
            or SettlementCaseState.Invoiced
            or SettlementCaseState.Cleared
            or SettlementCaseState.Disbursed;

    /// <summary>
    /// Creates a new append-only reversal case linked to <paramref name="original"/>. The reversal
    /// stores the operator <paramref name="reason"/> and <paramref name="reversedByUserId"/>. The
    /// inverted journal is posted separately on the event log (the case itself holds no journal).
    /// </summary>
    public static SettlementCase StartReversal(
        Guid id,
        SettlementCase original,
        string externalTransactionId,
        string reason,
        Guid? reversedByUserId,
        DateTime createdAt)
    {
        Check.NotNull(original, nameof(original));

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < SettlementReversalErrorCodes.MinReasonLength)
        {
            throw new BusinessException(SettlementReversalErrorCodes.ReasonRequired)
                .WithData("MinLength", SettlementReversalErrorCodes.MinReasonLength);
        }

        var reversal = new SettlementCase(id, original.Book, original.PartnerId, externalTransactionId?.Trim() ?? string.Empty, createdAt);
        reversal.LinkReversal(original.Id);
        reversal.Reason = reason.Trim();
        reversal.ReversedByUserId = reversedByUserId;
        return reversal;
    }

    public static string BuildIdempotencyKey(SettlementBook book, string externalTransactionId) =>
        $"stl:{book}:{(externalTransactionId ?? string.Empty).Trim()}".ToLowerInvariant();
}
