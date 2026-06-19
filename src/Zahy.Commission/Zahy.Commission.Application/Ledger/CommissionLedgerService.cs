using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Zahy.Commission;

public class CommissionLedgerService : ApplicationService, ICommissionLedgerService
{
    private readonly IRepository<CommissionLedgerEntry, Guid> _ledgerRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICommissionLedgerFinanceTrigger _financeTrigger;

    public CommissionLedgerService(
        IRepository<CommissionLedgerEntry, Guid> ledgerRepository,
        IGuidGenerator guidGenerator,
        ICommissionLedgerFinanceTrigger financeTrigger)
    {
        _ledgerRepository = ledgerRepository;
        _guidGenerator = guidGenerator;
        _financeTrigger = financeTrigger;
    }

    [UnitOfWork]
    public virtual async Task<CommissionLedgerAccrualResult> AccrueAsync(
        CommissionAccrualRequest request,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = CommissionLedgerIdempotency.BuildAccrualKey(
            request.SourceType,
            request.SourceId,
            request.RuleId);

        var existing = await FindByIdempotencyKeyAsync(idempotencyKey);
        if (existing != null)
        {
            return ToAccrualResult(existing, isNew: false);
        }

        var entry = CommissionLedgerEntry.CreateAccrual(
            _guidGenerator.Create(),
            request.PartnerId,
            request.TenantId,
            request.RuleId,
            request.SourceType,
            request.SourceId,
            request.BasisAmount,
            request.ComputedCommission,
            request.Direction,
            Clock.Now,
            request.OrderRecordId,
            request.Currency);

        await _ledgerRepository.InsertAsync(entry, autoSave: true, cancellationToken: cancellationToken);
        var result = ToAccrualResult(entry, isNew: true);
        await NotifyFinanceAsync(entry, result, request, cancellationToken);
        return result;
    }

    [UnitOfWork]
    public virtual async Task<CommissionLedgerEntryDto> ApproveAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var entry = await GetAccrualEntryAsync(entryId);
        entry.Approve(Clock.Now);
        await _ledgerRepository.UpdateAsync(entry, autoSave: true, cancellationToken: cancellationToken);
        return ToDto(entry);
    }

    [UnitOfWork]
    public virtual async Task<CommissionLedgerEntryDto> MarkPaidAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var entry = await GetAccrualEntryAsync(entryId);
        entry.MarkPaid(Clock.Now);
        await _ledgerRepository.UpdateAsync(entry, autoSave: true, cancellationToken: cancellationToken);
        return ToDto(entry);
    }

    [UnitOfWork]
    public virtual async Task<CommissionLedgerReversalResult> ReverseAsync(
        Guid originalEntryId,
        CancellationToken cancellationToken = default)
    {
        var original = await _ledgerRepository.FindAsync(originalEntryId, cancellationToken: cancellationToken);
        if (original == null)
        {
            throw new Volo.Abp.BusinessException(CommissionErrorCodes.LedgerEntryNotFound)
                .WithData("EntryId", originalEntryId);
        }

        var reversalKey = CommissionLedgerIdempotency.BuildReversalKey(
            original.SourceType,
            original.SourceId,
            original.RuleId,
            original.Id);

        var existingReversal = await FindByIdempotencyKeyAsync(reversalKey);
        if (existingReversal != null)
        {
            return ToReversalResult(original, existingReversal, isNew: false);
        }

        var reversal = CommissionLedgerEntry.CreateReversal(
            _guidGenerator.Create(),
            original,
            Clock.Now);

        await _ledgerRepository.InsertAsync(reversal, autoSave: true, cancellationToken: cancellationToken);
        return ToReversalResult(original, reversal, isNew: true);
    }

    private async Task<CommissionLedgerEntry> GetAccrualEntryAsync(Guid entryId)
    {
        var entry = await _ledgerRepository.FindAsync(entryId);
        if (entry == null)
        {
            throw new Volo.Abp.BusinessException(CommissionErrorCodes.LedgerEntryNotFound)
                .WithData("EntryId", entryId);
        }

        return entry;
    }

    private async Task<CommissionLedgerEntry?> FindByIdempotencyKeyAsync(string idempotencyKey)
    {
        var queryable = await _ledgerRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey);
    }

    private static CommissionLedgerAccrualResult ToAccrualResult(CommissionLedgerEntry entry, bool isNew) =>
        new()
        {
            EntryId = entry.Id,
            IsNew = isNew,
            Direction = entry.Direction,
            BasisAmount = entry.BasisAmount,
            ComputedCommission = entry.ComputedCommission,
            Status = entry.Status
        };

    private static CommissionLedgerReversalResult ToReversalResult(
        CommissionLedgerEntry original,
        CommissionLedgerEntry reversal,
        bool isNew) =>
        new()
        {
            ReversalEntryId = reversal.Id,
            OriginalEntryId = original.Id,
            IsNew = isNew,
            OriginalComputedCommission = original.ComputedCommission,
            ReversalComputedCommission = reversal.ComputedCommission
        };

    private static CommissionLedgerEntryDto ToDto(CommissionLedgerEntry entry) =>
        new()
        {
            Id = entry.Id,
            Direction = entry.Direction,
            BasisAmount = entry.BasisAmount,
            ComputedCommission = entry.ComputedCommission,
            Status = entry.Status,
            EntryKind = entry.EntryKind,
            ApprovedAt = entry.ApprovedAt,
            PaidAt = entry.PaidAt
        };

    private Task NotifyFinanceAsync(
        CommissionLedgerEntry entry,
        CommissionLedgerAccrualResult result,
        CommissionAccrualRequest request,
        CancellationToken cancellationToken) =>
        _financeTrigger.NotifyAccruedAsync(new CommissionLedgerFinanceAccrualContext
        {
            EntryId = entry.Id,
            IsNew = result.IsNew,
            PartnerId = entry.PartnerId,
            TenantId = entry.TenantId,
            ComputedCommission = entry.ComputedCommission,
            Direction = entry.Direction,
            EntryKind = entry.EntryKind,
            Currency = entry.Currency,
            SourceType = request.SourceType,
            SourceId = request.SourceId
        }, cancellationToken);
}
