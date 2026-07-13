using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.Settlement.AggregatorReconciliation;

/// <summary>
/// Aggregator statement reconciliation (Gate 1 of the partner-ops track). COMPUTE-ONLY: imports a
/// periodic aggregator statement, matches it against the reflected Order Ledger, queues classified
/// variance exceptions, and lets accountants resolve (note-mandatory) and close (two-person) — it
/// posts no journal and moves no money. View = Settlement.Read; commands = Settlement.Reconcile.
/// </summary>
public interface IAggregatorReconciliationAppService : IApplicationService
{
    /// <summary>Idempotent by content hash: re-importing identical content returns the existing
    /// statement with IsNew=false and inserts zero rows.</summary>
    Task<ImportAggregatorStatementResult> ImportAsync(ImportAggregatorStatementRequest request);

    /// <summary>Runs matching (auto-propose): Imported → Matching → Reconciled | HasExceptions.</summary>
    Task<AggregatorStatementDetailDto> MatchAsync(Guid statementId);

    /// <summary>Resolves one exception with a MANDATORY note; audited.</summary>
    Task<AggregatorStatementExceptionDto> ResolveExceptionAsync(ResolveAggregatorExceptionRequest request);

    /// <summary>Human commit (two-person gated, zero open exceptions); audited.</summary>
    Task<AggregatorStatementDto> CloseAsync(CloseAggregatorStatementRequest request);

    Task<List<AggregatorStatementDto>> GetListAsync();

    Task<AggregatorStatementDetailDto> GetAsync(Guid statementId);
}
