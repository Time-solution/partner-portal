using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Settlement;

/// <summary>
/// Read seam feeding statement matching: reflected orders for a partner+period joined with their
/// gross (reflected Order Ledger) and the snapshot BUY leg — over the Connectors-phase external-id
/// mapping. Implementations QUERY existing rows only (ReflectedPartnerOrder / OrderRecord /
/// SettlementCostMarkupSnapshot); they never create a mapping table and never mutate anything.
/// </summary>
public interface IAggregatorReflectedOrderSource
{
    Task<IReadOnlyList<ReflectedOrderMatchView>> GetForPartnerPeriodAsync(
        Guid partnerId,
        DateTime periodFrom,
        DateTime periodTo,
        CancellationToken cancellationToken = default);
}

/// <summary>Persistence seam for aggregator statements (EF adapter in EFCore; in-memory fakes in tests
/// — the ISettlementCaseStore pattern).</summary>
public interface IAggregatorStatementStore
{
    Task<AggregatorStatement?> FindByImportKeyAsync(string importIdempotencyKey, CancellationToken cancellationToken = default);

    Task<AggregatorStatement?> FindAsync(Guid statementId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AggregatorStatement>> GetListAsync(CancellationToken cancellationToken = default);

    Task InsertAsync(AggregatorStatement statement, IReadOnlyList<AggregatorStatementLine> lines, CancellationToken cancellationToken = default);

    Task UpdateAsync(AggregatorStatement statement, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AggregatorStatementLine>> GetLinesAsync(Guid statementId, CancellationToken cancellationToken = default);

    Task InsertExceptionsAsync(IReadOnlyList<AggregatorStatementException> exceptions, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AggregatorStatementException>> GetExceptionsAsync(Guid statementId, CancellationToken cancellationToken = default);

    Task<AggregatorStatementException?> FindExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default);

    Task UpdateExceptionAsync(AggregatorStatementException exception, CancellationToken cancellationToken = default);
}
