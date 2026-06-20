using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;

namespace Zahy.Settlement;

public interface ISettlementWebhookIngestionService
{
    /// <summary>Verify + accept (the 2xx ack). Bad/missing signature is rejected and logged.</summary>
    Task<SettlementWebhookAck> AcknowledgeAsync(InboundSettlementWebhook webhook, CancellationToken cancellationToken = default);

    /// <summary>The async pipeline: dedupe → route → allocate → post journal → Collected→Allocated → log. Retry-safe.</summary>
    Task<SettlementIngestResult> ProcessAsync(InboundSettlementWebhook webhook, CancellationToken cancellationToken = default);
}

/// <summary>
/// Inbound settlement webhook ingestion. Moves no money: the furthest it advances is Collected→Allocated.
/// Idempotent by (Book, ExternalEventId): a replay produces no second case/journal/transition.
/// </summary>
public sealed class SettlementWebhookIngestionService : ISettlementWebhookIngestionService
{
    private readonly ISettlementWebhookSignatureVerifier _verifier;
    private readonly ISettlementFlowProfileResolver _profileResolver;
    private readonly ISettlementAllocator _allocator;
    private readonly ISettlementCaseStore _caseStore;
    private readonly ISettlementEventStore _eventStore;

    public SettlementWebhookIngestionService(
        ISettlementWebhookSignatureVerifier verifier,
        ISettlementFlowProfileResolver profileResolver,
        ISettlementAllocator allocator,
        ISettlementCaseStore caseStore,
        ISettlementEventStore eventStore)
    {
        _verifier = verifier;
        _profileResolver = profileResolver;
        _allocator = allocator;
        _caseStore = caseStore;
        _eventStore = eventStore;
    }

    public async Task<SettlementWebhookAck> AcknowledgeAsync(InboundSettlementWebhook webhook, CancellationToken cancellationToken = default)
    {
        var status = _verifier.Verify(webhook);
        if (status != WebhookSignatureStatus.Valid)
        {
            await LogRejectedAsync(webhook, status, cancellationToken);
            return new SettlementWebhookAck { Accepted = false, SignatureStatus = status, Reason = $"Signature {status}" };
        }

        return new SettlementWebhookAck { Accepted = true, SignatureStatus = status };
    }

    public async Task<SettlementIngestResult> ProcessAsync(InboundSettlementWebhook webhook, CancellationToken cancellationToken = default)
    {
        var status = _verifier.Verify(webhook);
        if (status != WebhookSignatureStatus.Valid)
        {
            await LogRejectedAsync(webhook, status, cancellationToken);
            return new SettlementIngestResult { Outcome = SettlementEventOutcome.Rejected, SignatureStatus = status, Reason = $"Signature {status}" };
        }

        if (!webhook.PartnerKnown)
        {
            return await QuarantineAsync(webhook, status, "Unknown partner", cancellationToken);
        }

        ISettlementFlowProfile profile;
        try
        {
            profile = _profileResolver.Resolve(webhook.Book);
        }
        catch (BusinessException)
        {
            return await QuarantineAsync(webhook, status, $"Unmapped book {webhook.Book}", cancellationToken);
        }

        var existing = await _caseStore.FindByKeyAsync(webhook.Book, webhook.ExternalEventId, cancellationToken);
        if (existing != null)
        {
            await _eventStore.InsertAsync(SettlementWebhookEvent.Processed(
                Guid.NewGuid(), webhook.Book, webhook.PartnerId, webhook.ExternalEventId, webhook.Payload,
                status, SettlementEventOutcome.DuplicateIgnored, existing.Id, existing.State, null, webhook.ReceivedAt),
                cancellationToken);

            return new SettlementIngestResult
            {
                Outcome = SettlementEventOutcome.DuplicateIgnored,
                SettlementCaseId = existing.Id,
                State = existing.State,
                SignatureStatus = status
            };
        }

        var allocation = _allocator.Allocate(webhook.Allocation, profile, webhook.ReceivedAt);

        var settlementCase = SettlementCase.Start(Guid.NewGuid(), webhook.Book, webhook.PartnerId, webhook.ExternalEventId, webhook.ReceivedAt);
        settlementCase.TransitionTo(SettlementCaseState.Allocated, webhook.ReceivedAt);
        await _caseStore.InsertAsync(settlementCase, cancellationToken);

        var snapshot = SettlementAllocationSnapshot.From(allocation).ToJson();
        await _eventStore.InsertAsync(SettlementWebhookEvent.Processed(
            Guid.NewGuid(), webhook.Book, webhook.PartnerId, webhook.ExternalEventId, webhook.Payload,
            status, SettlementEventOutcome.Allocated, settlementCase.Id, settlementCase.State, snapshot, webhook.ReceivedAt),
            cancellationToken);

        return new SettlementIngestResult
        {
            Outcome = SettlementEventOutcome.Allocated,
            SettlementCaseId = settlementCase.Id,
            State = settlementCase.State,
            SignatureStatus = status
        };
    }

    private Task LogRejectedAsync(InboundSettlementWebhook webhook, WebhookSignatureStatus status, CancellationToken cancellationToken) =>
        _eventStore.InsertAsync(SettlementWebhookEvent.Rejected(
            Guid.NewGuid(), webhook.Book, webhook.PartnerId, webhook.ExternalEventId, webhook.Payload,
            status, $"Signature {status}", webhook.ReceivedAt), cancellationToken);

    private async Task<SettlementIngestResult> QuarantineAsync(InboundSettlementWebhook webhook, WebhookSignatureStatus status, string reason, CancellationToken cancellationToken)
    {
        await _eventStore.InsertAsync(SettlementWebhookEvent.Quarantined(
            Guid.NewGuid(), webhook.ExternalEventId, webhook.Payload, status, reason, webhook.ReceivedAt), cancellationToken);

        return new SettlementIngestResult { Outcome = SettlementEventOutcome.Quarantined, SignatureStatus = status, Reason = reason };
    }
}
