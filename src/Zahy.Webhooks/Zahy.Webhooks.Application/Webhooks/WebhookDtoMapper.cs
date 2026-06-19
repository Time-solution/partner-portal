using System.Linq;

namespace Zahy.Webhooks;

public static class WebhookDtoMapper
{
    public static WebhookSubscriptionDto ToDto(WebhookSubscription subscription)
    {
        var rules = subscription.GetFilterRules();
        return new WebhookSubscriptionDto
        {
            Id = subscription.Id,
            PartnerId = subscription.PartnerId,
            TargetUrl = subscription.TargetUrl,
            EventTypes = subscription.GetEventTypes().ToList(),
            FilterRules = rules == null
                ? null
                : new WebhookFilterRulesDto
                {
                    TenantIds = rules.TenantIds,
                    Directions = rules.Directions,
                    MinAmountSar = rules.MinAmountSar
                },
            Status = subscription.Status,
            CreationTime = subscription.CreationTime
        };
    }

    public static WebhookDeliveryDto ToDto(WebhookDelivery delivery) =>
        new()
        {
            Id = delivery.Id,
            OutboxMessageId = delivery.OutboxMessageId,
            SubscriptionId = delivery.SubscriptionId,
            PartnerId = delivery.PartnerId,
            AttemptNumber = delivery.AttemptNumber,
            Status = delivery.Status,
            HttpStatusCode = delivery.HttpStatusCode,
            ResponseBodySnippet = delivery.ResponseBodySnippet,
            DurationMs = delivery.DurationMs,
            ErrorCode = delivery.ErrorCode,
            AttemptedAt = delivery.AttemptedAt
        };

    public static WebhookDeadLetterDto ToDto(WebhookDeadLetter deadLetter) =>
        new()
        {
            Id = deadLetter.Id,
            OutboxMessageId = deadLetter.OutboxMessageId,
            PartnerId = deadLetter.PartnerId,
            EventType = deadLetter.EventType,
            Reason = deadLetter.Reason,
            FinalAttemptAt = deadLetter.FinalAttemptAt,
            ReplayedAt = deadLetter.ReplayedAt
        };

    public static WebhookFilterRules? ToDomain(WebhookFilterRulesDto? dto)
    {
        if (dto == null)
        {
            return null;
        }

        return new WebhookFilterRules
        {
            TenantIds = dto.TenantIds,
            Directions = dto.Directions,
            MinAmountSar = dto.MinAmountSar
        };
    }
}
