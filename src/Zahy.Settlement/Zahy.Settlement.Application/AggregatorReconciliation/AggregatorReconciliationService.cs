using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Guids;
using Zahy.Identity.Auditing;
using Zahy.Settlement.AggregatorReconciliation;

namespace Zahy.Settlement;

/// <summary>
/// Orchestrates statement reconciliation over the domain + stores. A PLAIN service (the P4
/// ingestion-service pattern) so tests drive it with in-memory fakes and explicit actor strings;
/// authorization + current-user resolution live in the thin app service. COMPUTE-ONLY: no journal,
/// no posting, no money movement; reads of snapshots/reflected orders are queries, never mutations.
/// </summary>
public sealed class AggregatorReconciliationService
{
    public const string AuditImport = "AggregatorStatement.Import";
    public const string AuditMatch = "AggregatorStatement.Match";
    public const string AuditResolveException = "AggregatorStatement.ResolveException";
    public const string AuditClose = "AggregatorStatement.Close";
    private const string AuditTargetType = "AggregatorStatement";

    private readonly IAggregatorStatementStore _store;
    private readonly IAggregatorReflectedOrderSource _reflectedOrders;
    private readonly IAdminAuditLogger _auditLogger;
    private readonly IGuidGenerator _guidGenerator;

    public AggregatorReconciliationService(
        IAggregatorStatementStore store,
        IAggregatorReflectedOrderSource reflectedOrders,
        IAdminAuditLogger auditLogger,
        IGuidGenerator guidGenerator)
    {
        _store = store;
        _reflectedOrders = reflectedOrders;
        _auditLogger = auditLogger;
        _guidGenerator = guidGenerator;
    }

    /// <summary>Content-hash idempotent: identical content returns the existing statement, inserts nothing.</summary>
    public async Task<ImportAggregatorStatementResult> ImportAsync(
        ImportAggregatorStatementRequest request,
        string actor,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(request, nameof(request));

        var hash = AggregatorStatementContentHash.Compute(
            request.PartnerId,
            request.Source,
            request.PeriodFrom,
            request.PeriodTo,
            request.DeclaredGross,
            request.DeclaredFees,
            request.DeclaredNet,
            request.Currency,
            request.Lines.Select(l => new AggregatorStatementContentHash.LineContent(
                l.ExternalOrderRef, l.OrderDate, l.Gross, l.AggregatorFee, l.Net)));

        var existing = await _store.FindByImportKeyAsync(hash, cancellationToken);
        if (existing != null)
        {
            return new ImportAggregatorStatementResult { StatementId = existing.Id, IsNew = false };
        }

        var statement = AggregatorStatement.Import(
            _guidGenerator.Create(),
            request.PartnerId,
            request.Source,
            request.PeriodFrom,
            request.PeriodTo,
            hash,
            request.DeclaredGross,
            request.DeclaredFees,
            request.DeclaredNet,
            request.Currency,
            actor,
            now,
            request.Lines.Count);

        var lines = request.Lines
            .Select(l => AggregatorStatementLine.Create(
                _guidGenerator.Create(), statement.Id, l.ExternalOrderRef, l.OrderDate, l.Gross, l.AggregatorFee, l.Net))
            .ToList();

        await _store.InsertAsync(statement, lines, cancellationToken);
        await _auditLogger.LogAsync(AuditImport, AuditTargetType, statement.Id.ToString("D"),
            extraData: $"source={statement.Source};lines={lines.Count};hash={hash}");

        return new ImportAggregatorStatementResult { StatementId = statement.Id, IsNew = true };
    }

    /// <summary>Auto-propose: runs the matcher and persists classified exception rows.
    /// Imported → Matching → Reconciled | HasExceptions.</summary>
    public async Task MatchAsync(Guid statementId, DateTime now, CancellationToken cancellationToken = default)
    {
        var statement = await GetRequiredAsync(statementId, cancellationToken);

        statement.BeginMatching();

        var lines = await _store.GetLinesAsync(statementId, cancellationToken);
        var reflected = await _reflectedOrders.GetForPartnerPeriodAsync(
            statement.PartnerId, statement.PeriodFrom, statement.PeriodTo, cancellationToken);

        var outcome = AggregatorStatementMatcher.Match(statement, lines, reflected);

        var exceptions = outcome.Variances
            .Select(v => AggregatorStatementException.Create(
                _guidGenerator.Create(), statement.Id, v.StatementLineId, v.Type,
                v.ExternalOrderRef, v.ExpectedAmount, v.ActualAmount, v.Details))
            .ToList();

        if (exceptions.Count > 0)
        {
            await _store.InsertExceptionsAsync(exceptions, cancellationToken);
        }

        statement.CompleteMatching(outcome.HasVariances, now);
        await _store.UpdateAsync(statement, cancellationToken);

        await _auditLogger.LogAsync(AuditMatch, AuditTargetType, statement.Id.ToString("D"),
            extraData: $"matched={outcome.MatchedLineIds.Count};exceptions={exceptions.Count};computedNet={outcome.ComputedNetTransferred}");
    }

    /// <summary>Override-with-mandatory-note (domain-enforced), audited. Blocked once immutable.</summary>
    public async Task<AggregatorStatementException> ResolveExceptionAsync(
        Guid exceptionId,
        string note,
        string actor,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var exception = await _store.FindExceptionAsync(exceptionId, cancellationToken)
            ?? throw new BusinessException(SettlementAggregatorStatementErrorCodes.StatementImportInvalid)
                .WithData("Reason", "ExceptionNotFound")
                .WithData("ExceptionId", exceptionId);

        var statement = await GetRequiredAsync(exception.StatementId, cancellationToken);

        // Closed statements are immutable; resolution happens in HasExceptions only.
        if (statement.IsClosed)
        {
            throw new BusinessException(SettlementAggregatorStatementErrorCodes.StatementImmutable)
                .WithData("StatementId", statement.Id)
                .WithData("Status", statement.Status.ToString());
        }

        exception.Resolve(note, actor, now);
        await _store.UpdateExceptionAsync(exception, cancellationToken);

        await _auditLogger.LogAsync(AuditResolveException, AuditTargetType, statement.Id.ToString("D"),
            extraData: $"exceptionId={exception.Id:D};type={exception.Type};by={actor}");

        return exception;
    }

    /// <summary>Human commit: zero open exceptions + two-person (resolver ≠ closer), domain-enforced. Audited.</summary>
    public async Task<AggregatorStatement> CloseAsync(
        Guid statementId,
        string actor,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var statement = await GetRequiredAsync(statementId, cancellationToken);
        var exceptions = await _store.GetExceptionsAsync(statementId, cancellationToken);

        var openCount = exceptions.Count(e => !e.Resolved);
        var resolvers = exceptions
            .Where(e => e.Resolved && !string.IsNullOrWhiteSpace(e.ResolvedBy))
            .Select(e => e.ResolvedBy!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        statement.Close(actor, resolvers, openCount, now);
        await _store.UpdateAsync(statement, cancellationToken);

        await _auditLogger.LogAsync(AuditClose, AuditTargetType, statement.Id.ToString("D"),
            extraData: $"by={actor};resolvedExceptions={exceptions.Count - openCount}");

        return statement;
    }

    private async Task<AggregatorStatement> GetRequiredAsync(Guid statementId, CancellationToken cancellationToken) =>
        await _store.FindAsync(statementId, cancellationToken)
            ?? throw new BusinessException(SettlementAggregatorStatementErrorCodes.StatementImportInvalid)
                .WithData("Reason", "StatementNotFound")
                .WithData("StatementId", statementId);
}
