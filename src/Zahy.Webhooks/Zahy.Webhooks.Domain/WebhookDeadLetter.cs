using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Webhooks;

public class WebhookDeadLetter : Entity<Guid>
{
    public Guid OutboxMessageId { get; private set; }

    public Guid PartnerId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    public DateTime FinalAttemptAt { get; private set; }

    public DateTime? ReplayedAt { get; private set; }

    protected WebhookDeadLetter()
    {
    }

    public WebhookDeadLetter(
        Guid id,
        Guid outboxMessageId,
        Guid partnerId,
        string eventType,
        string payloadJson,
        string reason,
        DateTime finalAttemptAt)
    {
        Id = id;
        OutboxMessageId = outboxMessageId;
        PartnerId = partnerId;
        EventType = Check.NotNullOrWhiteSpace(eventType, nameof(eventType));
        PayloadJson = Check.NotNull(payloadJson, nameof(payloadJson));
        Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason));
        FinalAttemptAt = finalAttemptAt;
    }

    public void MarkReplayed(DateTime replayedAt) => ReplayedAt = replayedAt;
}
