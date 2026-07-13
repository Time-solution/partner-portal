using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Uow;
using Zahy.Identity.Permissions;
using Zahy.Settlement.AggregatorReconciliation;

namespace Zahy.Settlement;

/// <summary>
/// Thin authorized surface over <see cref="AggregatorReconciliationService"/>: resolves the current
/// actor and delegates — every invariant (idempotency, note-mandatory, two-person, immutability)
/// lives in the DOMAIN. View = Settlement.Read (accountant + admin); every command =
/// Settlement.Reconcile. Partner/merchant roles hold neither, so v1 exposes nothing to them.
/// </summary>
public class AggregatorReconciliationAppService : ApplicationService, IAggregatorReconciliationAppService
{
    private readonly AggregatorReconciliationService _service;
    private readonly IAggregatorStatementStore _store;

    public AggregatorReconciliationAppService(
        AggregatorReconciliationService service,
        IAggregatorStatementStore store)
    {
        _service = service;
        _store = store;
    }

    [Authorize(ZahyPermissions.Settlement.Reconcile)]
    [UnitOfWork]
    public virtual Task<ImportAggregatorStatementResult> ImportAsync(ImportAggregatorStatementRequest request) =>
        _service.ImportAsync(request, CurrentActor(), Clock.Now);

    [Authorize(ZahyPermissions.Settlement.Reconcile)]
    [UnitOfWork]
    public virtual async Task<AggregatorStatementDetailDto> MatchAsync(Guid statementId)
    {
        await _service.MatchAsync(statementId, Clock.Now);
        return await GetAsync(statementId);
    }

    [Authorize(ZahyPermissions.Settlement.Reconcile)]
    [UnitOfWork]
    public virtual async Task<AggregatorStatementExceptionDto> ResolveExceptionAsync(ResolveAggregatorExceptionRequest request)
    {
        Check.NotNull(request, nameof(request));
        var resolved = await _service.ResolveExceptionAsync(request.ExceptionId, request.Note, CurrentActor(), Clock.Now);
        return MapException(resolved);
    }

    [Authorize(ZahyPermissions.Settlement.Reconcile)]
    [UnitOfWork]
    public virtual async Task<AggregatorStatementDto> CloseAsync(CloseAggregatorStatementRequest request)
    {
        Check.NotNull(request, nameof(request));
        var closed = await _service.CloseAsync(request.StatementId, CurrentActor(), Clock.Now);
        var exceptions = await _store.GetExceptionsAsync(closed.Id);
        var lines = await _store.GetLinesAsync(closed.Id);
        return MapStatement(closed, lines.Count, exceptions.Count(e => !e.Resolved));
    }

    [Authorize(ZahyPermissions.Settlement.Read)]
    public virtual async Task<List<AggregatorStatementDto>> GetListAsync()
    {
        var statements = await _store.GetListAsync();
        var result = new List<AggregatorStatementDto>(statements.Count);
        foreach (var statement in statements)
        {
            var lines = await _store.GetLinesAsync(statement.Id);
            var exceptions = await _store.GetExceptionsAsync(statement.Id);
            result.Add(MapStatement(statement, lines.Count, exceptions.Count(e => !e.Resolved)));
        }

        return result;
    }

    [Authorize(ZahyPermissions.Settlement.Read)]
    public virtual async Task<AggregatorStatementDetailDto> GetAsync(Guid statementId)
    {
        var statement = await _store.FindAsync(statementId)
            ?? throw new BusinessException(SettlementAggregatorStatementErrorCodes.StatementImportInvalid)
                .WithData("Reason", "StatementNotFound")
                .WithData("StatementId", statementId);

        var lines = await _store.GetLinesAsync(statementId);
        var exceptions = await _store.GetExceptionsAsync(statementId);
        var exceptionLineIds = exceptions
            .Where(e => e.StatementLineId != null)
            .Select(e => e.StatementLineId!.Value)
            .ToHashSet();

        return new AggregatorStatementDetailDto
        {
            Statement = MapStatement(statement, lines.Count, exceptions.Count(e => !e.Resolved)),
            Lines = lines.Select(l => new AggregatorStatementLineDto
            {
                Id = l.Id,
                ExternalOrderRef = l.ExternalOrderRef,
                OrderDate = l.OrderDate,
                Gross = l.Gross,
                AggregatorFee = l.AggregatorFee,
                Net = l.Net,
                Matched = statement.MatchedAt != null && !exceptionLineIds.Contains(l.Id)
            }).ToList(),
            Exceptions = exceptions.Select(MapException).ToList()
        };
    }

    /// <summary>Stable actor identity for audit + the two-person compare (user id, else username).</summary>
    private string CurrentActor() =>
        CurrentUser.Id?.ToString("D") ?? CurrentUser.UserName ?? "unknown";

    private static AggregatorStatementDto MapStatement(AggregatorStatement s, int lineCount, int openExceptionCount) =>
        new()
        {
            Id = s.Id,
            PartnerId = s.PartnerId,
            Source = s.Source,
            PeriodFrom = s.PeriodFrom,
            PeriodTo = s.PeriodTo,
            DeclaredGross = s.DeclaredGross,
            DeclaredFees = s.DeclaredFees,
            DeclaredNet = s.DeclaredNet,
            Currency = s.Currency,
            Status = s.Status.ToString(),
            ImportedAt = s.ImportedAt,
            MatchedAt = s.MatchedAt,
            ClosedAt = s.ClosedAt,
            ClosedBy = s.ClosedBy,
            LineCount = lineCount,
            OpenExceptionCount = openExceptionCount
        };

    private static AggregatorStatementExceptionDto MapException(AggregatorStatementException e) =>
        new()
        {
            Id = e.Id,
            StatementId = e.StatementId,
            StatementLineId = e.StatementLineId,
            Type = e.Type.ToString(),
            ExternalOrderRef = e.ExternalOrderRef,
            ExpectedAmount = e.ExpectedAmount,
            ActualAmount = e.ActualAmount,
            Details = e.Details,
            Resolved = e.Resolved,
            ResolutionNote = e.ResolutionNote,
            ResolvedBy = e.ResolvedBy,
            ResolvedAt = e.ResolvedAt
        };
}
