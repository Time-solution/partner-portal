using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Webhooks;

public class WebhookOutboxMessage : Entity<Guid>
{
    public Guid PartnerId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public WebhookOutboxStatus Status { get; private set; }

    public DateTime ScheduledAt { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    protected WebhookOutboxMessage()
    {
    }

    public WebhookOutboxMessage(
        Guid id,
        Guid partnerId,
        string eventType,
        string idempotencyKey,
        string payloadJson,
        DateTime createdAt)
    {
        Id = id;
        PartnerId = partnerId;
        EventType = Check.NotNullOrWhiteSpace(eventType, nameof(eventType));
        IdempotencyKey = Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));
        PayloadJson = Check.NotNull(payloadJson, nameof(payloadJson));
        Status = WebhookOutboxStatus.Pending;
        ScheduledAt = createdAt;
        AttemptCount = 0;
        CreatedAt = createdAt;
    }

    public void MarkProcessing() => Status = WebhookOutboxStatus.Processing;

    public void MarkCompleted() => Status = WebhookOutboxStatus.Completed;

    public void MarkDeadLettered() => Status = WebhookOutboxStatus.DeadLettered;

    public void ScheduleRetry(DateTime nextAttemptAt)
    {
        Status = WebhookOutboxStatus.Pending;
        ScheduledAt = nextAttemptAt;
        AttemptCount++;
    }

    public void MarkDueNow() => ScheduledAt = DateTime.UtcNow.AddMinutes(-1);
}
