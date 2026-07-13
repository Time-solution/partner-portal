using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Zahy.Settlement;

/// <summary>EF adapter for <see cref="IAggregatorStatementStore"/> (the ISettlementCaseStore pattern).</summary>
public class AggregatorStatementStore : IAggregatorStatementStore, ITransientDependency
{
    private readonly IRepository<AggregatorStatement, Guid> _statements;
    private readonly IRepository<AggregatorStatementLine, Guid> _lines;
    private readonly IRepository<AggregatorStatementException, Guid> _exceptions;

    public AggregatorStatementStore(
        IRepository<AggregatorStatement, Guid> statements,
        IRepository<AggregatorStatementLine, Guid> lines,
        IRepository<AggregatorStatementException, Guid> exceptions)
    {
        _statements = statements;
        _lines = lines;
        _exceptions = exceptions;
    }

    public async Task<AggregatorStatement?> FindByImportKeyAsync(string importIdempotencyKey, CancellationToken cancellationToken = default)
    {
        var queryable = await _statements.GetQueryableAsync();
        return queryable.FirstOrDefault(x => x.ImportIdempotencyKey == importIdempotencyKey);
    }

    public Task<AggregatorStatement?> FindAsync(Guid statementId, CancellationToken cancellationToken = default) =>
        _statements.FindAsync(statementId, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<AggregatorStatement>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var queryable = await _statements.GetQueryableAsync();
        return queryable.OrderByDescending(x => x.ImportedAt).ToList();
    }

    public async Task InsertAsync(AggregatorStatement statement, IReadOnlyList<AggregatorStatementLine> lines, CancellationToken cancellationToken = default)
    {
        await _statements.InsertAsync(statement, autoSave: true, cancellationToken: cancellationToken);
        await _lines.InsertManyAsync(lines, autoSave: true, cancellationToken: cancellationToken);
    }

    public Task UpdateAsync(AggregatorStatement statement, CancellationToken cancellationToken = default) =>
        _statements.UpdateAsync(statement, autoSave: true, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<AggregatorStatementLine>> GetLinesAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        var queryable = await _lines.GetQueryableAsync();
        return queryable.Where(x => x.StatementId == statementId).OrderBy(x => x.OrderDate).ToList();
    }

    public Task InsertExceptionsAsync(IReadOnlyList<AggregatorStatementException> exceptions, CancellationToken cancellationToken = default) =>
        _exceptions.InsertManyAsync(exceptions, autoSave: true, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<AggregatorStatementException>> GetExceptionsAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        var queryable = await _exceptions.GetQueryableAsync();
        return queryable.Where(x => x.StatementId == statementId).ToList();
    }

    public Task<AggregatorStatementException?> FindExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default) =>
        _exceptions.FindAsync(exceptionId, cancellationToken: cancellationToken);

    public Task UpdateExceptionAsync(AggregatorStatementException exception, CancellationToken cancellationToken = default) =>
        _exceptions.UpdateAsync(exception, autoSave: true, cancellationToken: cancellationToken);
}
