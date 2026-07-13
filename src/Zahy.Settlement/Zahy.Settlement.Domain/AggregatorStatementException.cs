using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// One classified variance surfaced by matching an aggregator statement against the reflected Order
/// Ledger. Resolution is an OVERRIDE-WITH-MANDATORY-NOTE (bank-reconcile discipline), recorded with
/// who/when and audited via IAdminAuditLogger at the application layer. Exception rows record
/// variance only — resolving one never mutates snapshots, reflected orders, or ledger rows.
/// </summary>
public class AggregatorStatementException : Entity<Guid>
{
    public Guid StatementId { get; private set; }

    /// <summary>Null for statement-level variances (e.g. NetTransferMismatch).</summary>
    public Guid? StatementLineId { get; private set; }

    public AggregatorVarianceType Type { get; private set; }

    /// <summary>External order ref the variance concerns; empty for statement-level rows.</summary>
    public string ExternalOrderRef { get; private set; } = string.Empty;

    /// <summary>Our side (reflected ledger / snapshot buy / computed net), when applicable.</summary>
    public decimal? ExpectedAmount { get; private set; }

    /// <summary>The statement's side, when applicable.</summary>
    public decimal? ActualAmount { get; private set; }

    public string Details { get; private set; } = string.Empty;

    public bool Resolved { get; private set; }

    public string? ResolutionNote { get; private set; }

    public string? ResolvedBy { get; private set; }

    public DateTime? ResolvedAt { get; private set; }

    protected AggregatorStatementException()
    {
    }

    public static AggregatorStatementException Create(
        Guid id,
        Guid statementId,
        Guid? statementLineId,
        AggregatorVarianceType type,
        string externalOrderRef,
        decimal? expectedAmount,
        decimal? actualAmount,
        string details)
    {
        return new AggregatorStatementException
        {
            Id = id,
            StatementId = statementId,
            StatementLineId = statementLineId,
            Type = type,
            ExternalOrderRef = (externalOrderRef ?? string.Empty).Trim(),
            ExpectedAmount = expectedAmount,
            ActualAmount = actualAmount,
            Details = (details ?? string.Empty).Trim()
        };
    }

    /// <summary>
    /// Human resolution with a MANDATORY note (:073 when blank) — mirrors ReconcileWithOverride.
    /// Resolving an already-resolved exception is an idempotent NO-OP (first resolution stands).
    /// </summary>
    public void Resolve(string note, string resolvedBy, DateTime at)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new BusinessException(SettlementAggregatorStatementErrorCodes.ExceptionResolutionRequiresNote)
                .WithData("ExceptionId", Id)
                .WithData("Type", Type.ToString());
        }

        if (Resolved)
        {
            return;
        }

        Resolved = true;
        ResolutionNote = note.Trim();
        ResolvedBy = (resolvedBy ?? string.Empty).Trim();
        ResolvedAt = at;
    }
}
