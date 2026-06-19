using System;
using Volo.Abp.Domain.Entities;

namespace Zahy.Webhooks;

public class WebhookDelivery : Entity<Guid>
{
    public Guid OutboxMessageId { get; private set; }

    public Guid SubscriptionId { get; private set; }

    public Guid PartnerId { get; private set; }

    public int AttemptNumber { get; private set; }

    public WebhookDeliveryAttemptStatus Status { get; private set; }

    public int? HttpStatusCode { get; private set; }

    public string? ResponseBodySnippet { get; private set; }

    public int DurationMs { get; private set; }

    public string? ErrorCode { get; private set; }

    public DateTime AttemptedAt { get; private set; }

    protected WebhookDelivery()
    {
    }

    public WebhookDelivery(
        Guid id,
        Guid outboxMessageId,
        Guid subscriptionId,
        Guid partnerId,
        int attemptNumber,
        WebhookDeliveryAttemptStatus status,
        int? httpStatusCode,
        string? responseBodySnippet,
        int durationMs,
        string? errorCode,
        DateTime attemptedAt)
    {
        Id = id;
        OutboxMessageId = outboxMessageId;
        SubscriptionId = subscriptionId;
        PartnerId = partnerId;
        AttemptNumber = attemptNumber;
        Status = status;
        HttpStatusCode = httpStatusCode;
        ResponseBodySnippet = Truncate(responseBodySnippet);
        DurationMs = durationMs;
        ErrorCode = errorCode;
        AttemptedAt = attemptedAt;
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= WebhookConsts.MaxResponseSnippetLength
            ? value
            : value[..WebhookConsts.MaxResponseSnippetLength];
    }
}
