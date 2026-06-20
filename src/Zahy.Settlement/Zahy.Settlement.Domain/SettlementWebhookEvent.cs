using System;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// Append-only log of every inbound settlement webhook (the dashboard's source of truth). Captures the
/// raw payload, signature status, the outcome (incl. dedupe result), which settlement/state it produced,
/// and a snapshot of the posted allocation — so the explain endpoint can trace webhook → settlement →
/// journal → state. Rows are never mutated.
/// </summary>
public class SettlementWebhookEvent : AggregateRoot<Guid>
{
    public SettlementBook? Book { get; private set; }

    public Guid? PartnerId { get; private set; }

    public string ExternalEventId { get; private set; } = string.Empty;

    public WebhookSignatureStatus SignatureStatus { get; private set; }

    public SettlementEventOutcome Outcome { get; private set; }

    public Guid? SettlementCaseId { get; private set; }

    public SettlementCaseState? ResultingState { get; private set; }

    public string RawPayload { get; private set; } = string.Empty;

    public string? Reason { get; private set; }

    /// <summary>Serialized snapshot of the posted journal/allocation (for the explain endpoint).</summary>
    public string? AllocationSnapshotJson { get; private set; }

    public DateTime ReceivedAt { get; private set; }

    protected SettlementWebhookEvent()
    {
    }

    private SettlementWebhookEvent(Guid id, string externalEventId, string rawPayload, DateTime receivedAt)
        : base(id)
    {
        ExternalEventId = externalEventId;
        RawPayload = rawPayload;
        ReceivedAt = receivedAt;
    }

    public static SettlementWebhookEvent Rejected(
        Guid id,
        SettlementBook? book,
        Guid? partnerId,
        string externalEventId,
        string rawPayload,
        WebhookSignatureStatus signatureStatus,
        string reason,
        DateTime receivedAt) =>
        new(id, externalEventId, rawPayload, receivedAt)
        {
            Book = book,
            PartnerId = partnerId,
            SignatureStatus = signatureStatus,
            Outcome = SettlementEventOutcome.Rejected,
            Reason = reason
        };

    public static SettlementWebhookEvent Quarantined(
        Guid id,
        string externalEventId,
        string rawPayload,
        WebhookSignatureStatus signatureStatus,
        string reason,
        DateTime receivedAt) =>
        new(id, externalEventId, rawPayload, receivedAt)
        {
            SignatureStatus = signatureStatus,
            Outcome = SettlementEventOutcome.Quarantined,
            Reason = reason
        };

    public static SettlementWebhookEvent Processed(
        Guid id,
        SettlementBook book,
        Guid partnerId,
        string externalEventId,
        string rawPayload,
        WebhookSignatureStatus signatureStatus,
        SettlementEventOutcome outcome,
        Guid settlementCaseId,
        SettlementCaseState resultingState,
        string? allocationSnapshotJson,
        DateTime receivedAt) =>
        new(id, externalEventId, rawPayload, receivedAt)
        {
            Book = book,
            PartnerId = partnerId,
            SignatureStatus = signatureStatus,
            Outcome = outcome,
            SettlementCaseId = settlementCaseId,
            ResultingState = resultingState,
            AllocationSnapshotJson = allocationSnapshotJson
        };
}
