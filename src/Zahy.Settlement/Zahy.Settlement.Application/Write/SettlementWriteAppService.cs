using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Zahy.Identity.Auditing;
using Zahy.Identity.Permissions;
using Zahy.Settlement.Read;

namespace Zahy.Settlement.Write;

/// <summary>
/// Triggers append-only SettlementCase reversals. Gated by the disburse-class permission (the same
/// privilege that governs Disbursement.Release), NOT the read permission. Money trail stays
/// append-only: a reversal is a new compensating case + inverted journal, never an in-place edit.
/// </summary>
public class SettlementWriteAppService : ApplicationService, ISettlementWriteAppService
{
    private readonly IRepository<SettlementCase, Guid> _caseRepository;
    private readonly ISettlementEventStore _eventStore;
    private readonly IAdminAuditLogger _auditLogger;

    public SettlementWriteAppService(
        IRepository<SettlementCase, Guid> caseRepository,
        ISettlementEventStore eventStore,
        IAdminAuditLogger auditLogger)
    {
        _caseRepository = caseRepository;
        _eventStore = eventStore;
        _auditLogger = auditLogger;
    }

    [Authorize(ZahyPermissions.Settlement.Disburse)]
    [UnitOfWork]
    public virtual async Task<SettlementCaseReadDto> TriggerCaseReversalAsync(TriggerReversalRequest request)
    {
        Check.NotNull(request, nameof(request));

        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length < SettlementReversalErrorCodes.MinReasonLength)
        {
            throw new BusinessException(SettlementReversalErrorCodes.ReasonRequired)
                .WithData("MinLength", SettlementReversalErrorCodes.MinReasonLength);
        }

        var original = await _caseRepository.GetAsync(request.OriginalSettlementCaseId);

        var reversalExternalTransactionId = BuildReversalExternalTransactionId(original, request.IdempotencyKey);

        // Idempotency: same original + same key resolves to the already-created reversal.
        var existing = (await _caseRepository.GetListAsync(x =>
                x.ReversesSettlementCaseId == original.Id &&
                x.ExternalTransactionId == reversalExternalTransactionId))
            .FirstOrDefault();
        if (existing != null)
        {
            return await MapAsync(existing);
        }

        // Eligibility: original must be in a reversible state and not already reversed (061).
        if (original.ReversesSettlementCaseId != null || !SettlementCase.IsReversibleState(original.State))
        {
            throw new BusinessException(SettlementReversalErrorCodes.OriginalNotReversible)
                .WithData("OriginalId", original.Id)
                .WithData("State", original.State.ToString());
        }

        var alreadyReversed = (await _caseRepository.GetListAsync(x =>
            x.ReversesSettlementCaseId == original.Id)).Any();
        if (alreadyReversed)
        {
            throw new BusinessException(SettlementReversalErrorCodes.OriginalNotReversible)
                .WithData("OriginalId", original.Id)
                .WithData("State", "AlreadyReversed");
        }

        var originalSnapshot = await LoadSnapshotAsync(original.Id);
        var invertedSnapshot = originalSnapshot?.Invert();

        if (originalSnapshot != null && invertedSnapshot != null &&
            !originalSnapshot.NetsToZeroWith(invertedSnapshot))
        {
            throw new BusinessException(SettlementErrorCodes.UnbalancedJournal)
                .WithData("OriginalId", original.Id);
        }

        var now = Clock.Now;
        var reversal = SettlementCase.StartReversal(
            GuidGenerator.Create(),
            original,
            reversalExternalTransactionId,
            request.Reason.Trim(),
            CurrentUser.Id,
            now);
        reversal.TransitionTo(SettlementCaseState.Allocated, now);
        await _caseRepository.InsertAsync(reversal, autoSave: true);

        if (invertedSnapshot != null)
        {
            await _eventStore.InsertAsync(SettlementWebhookEvent.Processed(
                GuidGenerator.Create(),
                original.Book,
                original.PartnerId,
                reversalExternalTransactionId,
                request.Reason.Trim(),
                WebhookSignatureStatus.Valid,
                SettlementEventOutcome.Allocated,
                reversal.Id,
                reversal.State,
                invertedSnapshot.ToJson(),
                now));
        }

        await _auditLogger.LogAsync(
            action: "Settlement.TriggerCaseReversal",
            targetType: nameof(SettlementCase),
            targetId: original.Id.ToString(),
            result: AdminAuditResults.Success,
            extraData: $"ReversalCaseId={reversal.Id}; Reason={request.Reason.Trim()}");

        return await MapAsync(reversal);
    }

    private static string BuildReversalExternalTransactionId(SettlementCase original, string? idempotencyKey)
    {
        var key = string.IsNullOrWhiteSpace(idempotencyKey)
            ? original.Id.ToString("N")
            : idempotencyKey.Trim();
        return $"reversal:{original.Id:N}:{key}";
    }

    private async Task<SettlementAllocationSnapshot?> LoadSnapshotAsync(Guid caseId)
    {
        var events = await _eventStore.GetByCaseAsync(caseId);
        var json = events
            .Where(e => e.Outcome == SettlementEventOutcome.Allocated && e.AllocationSnapshotJson != null)
            .Select(e => e.AllocationSnapshotJson)
            .FirstOrDefault();
        return SettlementAllocationSnapshot.FromJson(json);
    }

    private async Task<SettlementCaseReadDto> MapAsync(SettlementCase settlementCase)
    {
        var events = await _eventStore.GetByCaseAsync(settlementCase.Id);
        var allocationJson = events
            .Where(e => e.Outcome == SettlementEventOutcome.Allocated && e.AllocationSnapshotJson != null)
            .Select(e => e.AllocationSnapshotJson)
            .FirstOrDefault();
        var postedAt = events
            .Where(e => e.Outcome == SettlementEventOutcome.Allocated)
            .Select(e => (DateTime?)e.ReceivedAt)
            .FirstOrDefault();

        var journal = SettlementReadDtoMapper.ToJournalDto(
            SettlementAllocationSnapshot.FromJson(allocationJson),
            postedAt);

        return SettlementReadDtoMapper.ToDto(settlementCase, journal);
    }
}
