using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// An imported aggregator settlement statement (e.g. Jahez, Pattern A — delivery/fulfilment per
/// order) reconciled against our reflected Order Ledger with bank-reconcile discipline: the engine
/// AUTO-PROPOSES the match outcome (Reconciled | HasExceptions) and a HUMAN commits the close, gated
/// by the two-person rule (resolver ≠ closer) and zero open exceptions. COMPUTE-ONLY: this aggregate
/// posts no journal, moves no money, and never mutates snapshots, reflected orders, or ledger rows.
/// Reconciled/Closed statements are immutable — corrections are append-only (a new import).
/// </summary>
public class AggregatorStatement : AggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    /// <summary>Statement source system (e.g. "Jahez").</summary>
    public string Source { get; private set; } = string.Empty;

    public DateTime PeriodFrom { get; private set; }

    public DateTime PeriodTo { get; private set; }

    /// <summary>Content hash of the imported statement — re-importing identical content is a no-op.</summary>
    public string ImportIdempotencyKey { get; private set; } = string.Empty;

    public decimal DeclaredGross { get; private set; }

    /// <summary>The aggregator's declared total fees — OUR BUY SIDE (Zahy is principal), never revenue.</summary>
    public decimal DeclaredFees { get; private set; }

    public decimal DeclaredNet { get; private set; }

    public string Currency { get; private set; } = SettlementConsts.DefaultCurrency;

    public AggregatorStatementStatus Status { get; private set; }

    public DateTime ImportedAt { get; private set; }

    public string ImportedBy { get; private set; } = string.Empty;

    public DateTime? MatchedAt { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    public string? ClosedBy { get; private set; }

    /// <summary>Committed terminal state (human-gated).</summary>
    public bool IsClosed => Status == AggregatorStatementStatus.Closed;

    /// <summary>Reconciled/Closed statements are immutable (append-only corrections).</summary>
    public bool IsImmutable =>
        Status == AggregatorStatementStatus.Reconciled || Status == AggregatorStatementStatus.Closed;

    protected AggregatorStatement()
    {
    }

    private AggregatorStatement(Guid id) : base(id)
    {
    }

    public static AggregatorStatement Import(
        Guid id,
        Guid partnerId,
        string source,
        DateTime periodFrom,
        DateTime periodTo,
        string importIdempotencyKey,
        decimal declaredGross,
        decimal declaredFees,
        decimal declaredNet,
        string currency,
        string importedBy,
        DateTime importedAt,
        int lineCount)
    {
        if (string.IsNullOrWhiteSpace(source) ||
            string.IsNullOrWhiteSpace(importIdempotencyKey) ||
            periodTo < periodFrom ||
            lineCount <= 0)
        {
            throw new BusinessException(SettlementAggregatorStatementErrorCodes.StatementImportInvalid)
                .WithData("Source", source ?? "<null>")
                .WithData("PeriodFrom", periodFrom)
                .WithData("PeriodTo", periodTo)
                .WithData("LineCount", lineCount);
        }

        return new AggregatorStatement(id)
        {
            PartnerId = partnerId,
            Source = source.Trim(),
            PeriodFrom = periodFrom,
            PeriodTo = periodTo,
            ImportIdempotencyKey = importIdempotencyKey.Trim(),
            DeclaredGross = SettlementMoney.Round(declaredGross),
            DeclaredFees = SettlementMoney.Round(declaredFees),
            DeclaredNet = SettlementMoney.Round(declaredNet),
            Currency = currency.Trim().ToUpperInvariant(),
            Status = AggregatorStatementStatus.Imported,
            ImportedBy = (importedBy ?? string.Empty).Trim(),
            ImportedAt = importedAt
        };
    }

    /// <summary>Imported → Matching. Any other source status is an illegal transition.</summary>
    public void BeginMatching()
    {
        if (Status != AggregatorStatementStatus.Imported)
        {
            throw IllegalTransition(nameof(BeginMatching));
        }

        Status = AggregatorStatementStatus.Matching;
    }

    /// <summary>
    /// Matching → Reconciled (clean — the engine's PROPOSED outcome, still human-closed later) or
    /// Matching → HasExceptions (variances queued for audited human resolution).
    /// </summary>
    public void CompleteMatching(bool hasExceptions, DateTime at)
    {
        if (Status != AggregatorStatementStatus.Matching)
        {
            throw IllegalTransition(nameof(CompleteMatching));
        }

        Status = hasExceptions
            ? AggregatorStatementStatus.HasExceptions
            : AggregatorStatementStatus.Reconciled;
        MatchedAt = at;
    }

    /// <summary>
    /// Human COMMIT (the release-equivalent). Allowed from Reconciled or HasExceptions when:
    ///   (1) zero open exceptions (else :074), and
    ///   (2) the TWO-PERSON rule holds — the closer must not be any of the humans who resolved an
    ///       exception on this statement (else :075; compared by actor id, case-insensitive, mirroring
    ///       Disbursement.Release reconciler ≠ releaser).
    /// Closing an already-Closed statement is an idempotent NO-OP. Moves NO money, posts NO journal.
    /// </summary>
    public void Close(string closedBy, IReadOnlyCollection<string> exceptionResolverActors, int openExceptionCount, DateTime at)
    {
        if (IsClosed)
        {
            return;
        }

        if (Status != AggregatorStatementStatus.Reconciled && Status != AggregatorStatementStatus.HasExceptions)
        {
            throw IllegalTransition(nameof(Close));
        }

        if (openExceptionCount > 0)
        {
            throw new BusinessException(SettlementAggregatorStatementErrorCodes.CloseBlockedOpenExceptions)
                .WithData("StatementId", Id)
                .WithData("OpenExceptions", openExceptionCount);
        }

        var resolvers = exceptionResolverActors ?? Array.Empty<string>();
        if (resolvers.Any(resolver => IsSameActor(closedBy, resolver)))
        {
            throw new BusinessException(SettlementAggregatorStatementErrorCodes.CloseBlockedSameActorAsResolver)
                .WithData("StatementId", Id)
                .WithData("Actor", closedBy?.Trim());
        }

        Status = AggregatorStatementStatus.Closed;
        ClosedBy = (closedBy ?? string.Empty).Trim();
        ClosedAt = at;
    }

    /// <summary>Guard used by resolution flows: no mutation once Reconciled/Closed (:072).</summary>
    public void EnsureMutable()
    {
        if (IsImmutable)
        {
            throw new BusinessException(SettlementAggregatorStatementErrorCodes.StatementImmutable)
                .WithData("StatementId", Id)
                .WithData("Status", Status.ToString());
        }
    }

    private BusinessException IllegalTransition(string action) =>
        new BusinessException(SettlementAggregatorStatementErrorCodes.IllegalStatementTransition)
            .WithData("StatementId", Id)
            .WithData("Status", Status.ToString())
            .WithData("Action", action);

    private static bool IsSameActor(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
