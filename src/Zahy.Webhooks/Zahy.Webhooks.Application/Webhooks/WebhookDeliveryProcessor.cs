using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Zahy.Webhooks;

public class WebhookDeliveryProcessor : ApplicationService, IWebhookDeliveryProcessor
{
    private readonly IRepository<WebhookOutboxMessage, Guid> _outboxRepository;
    private readonly IRepository<WebhookSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<WebhookDelivery, Guid> _deliveryRepository;
    private readonly IRepository<WebhookDeadLetter, Guid> _deadLetterRepository;
    private readonly IWebhookOutboxLeaseService _outboxLeaseService;
    private readonly IWebhookDeliveryTransport _deliveryTransport;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IDataFilter _dataFilter;

    public WebhookDeliveryProcessor(
        IRepository<WebhookOutboxMessage, Guid> outboxRepository,
        IRepository<WebhookSubscription, Guid> subscriptionRepository,
        IRepository<WebhookDelivery, Guid> deliveryRepository,
        IRepository<WebhookDeadLetter, Guid> deadLetterRepository,
        IWebhookOutboxLeaseService outboxLeaseService,
        IWebhookDeliveryTransport deliveryTransport,
        IGuidGenerator guidGenerator,
        IDataFilter dataFilter)
    {
        _outboxRepository = outboxRepository;
        _subscriptionRepository = subscriptionRepository;
        _deliveryRepository = deliveryRepository;
        _deadLetterRepository = deadLetterRepository;
        _outboxLeaseService = outboxLeaseService;
        _deliveryTransport = deliveryTransport;
        _guidGenerator = guidGenerator;
        _dataFilter = dataFilter;
    }

    [UnitOfWork]
    public virtual async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = Clock.Now;
        var leaseDuration = TimeSpan.FromMinutes(WebhookConsts.DefaultOutboxLeaseMinutes);
        var claimedIds = await _outboxLeaseService.ClaimDueBatchAsync(
            WebhookConsts.DefaultOutboxClaimBatchSize,
            now,
            leaseDuration,
            cancellationToken);

        foreach (var messageId in claimedIds)
        {
            var message = await _outboxRepository.GetAsync(messageId, cancellationToken: cancellationToken);
            await ProcessMessageAsync(message, cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(WebhookOutboxMessage message, CancellationToken cancellationToken)
    {
        var subscriptions = await GetMatchingSubscriptionsAsync(message);
        if (subscriptions.Count == 0)
        {
            message.MarkCompleted();
            await _outboxRepository.UpdateAsync(message, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        var attemptNumber = message.AttemptCount + 1;
        var anyRetryableFailure = false;

        foreach (var subscription in subscriptions)
        {
            if (!subscription.GetEventTypes().Contains(message.EventType))
            {
                await LogDeliveryAsync(
                    message,
                    subscription,
                    attemptNumber,
                    WebhookDeliveryAttemptStatus.Skipped,
                    null,
                    null,
                    0,
                    "FilteredEventType",
                    cancellationToken);
                continue;
            }

            if (!WebhookPayloadFilter.Matches(subscription.GetFilterRules(), message.PayloadJson))
            {
                await LogDeliveryAsync(
                    message,
                    subscription,
                    attemptNumber,
                    WebhookDeliveryAttemptStatus.Skipped,
                    null,
                    null,
                    0,
                    "FilteredRules",
                    cancellationToken);
                continue;
            }

            var signature = WebhookHmacSigner.Sign(subscription.SigningSecret, message.PayloadJson);
            var result = await _deliveryTransport.SendAsync(new WebhookDeliveryRequest
            {
                DeliveryId = message.Id,
                SubscriptionId = subscription.Id,
                TargetUrl = subscription.TargetUrl,
                EventType = message.EventType,
                PayloadJson = message.PayloadJson,
                UnixTimestamp = signature.UnixTimestamp,
                Signature = signature.Signature
            }, cancellationToken);

            var status = result.Success
                ? WebhookDeliveryAttemptStatus.Succeeded
                : WebhookDeliveryAttemptStatus.Failed;

            await LogDeliveryAsync(
                message,
                subscription,
                attemptNumber,
                status,
                result.HttpStatusCode,
                result.ResponseBody,
                result.DurationMs,
                result.ErrorCode,
                cancellationToken);

            if (!result.Success && result.IsRetryable)
            {
                anyRetryableFailure = true;
            }
        }

        if (anyRetryableFailure)
        {
            if (WebhookRetryPolicy.HasExceededMaxAttempts(message.AttemptCount + 1))
            {
                await MoveToDeadLetterAsync(message, "MaxRetriesExceeded", cancellationToken);
            }
            else
            {
                message.ScheduleRetry(WebhookRetryPolicy.GetNextAttemptUtc(message.AttemptCount + 1, Clock.Now));
                await _outboxRepository.UpdateAsync(message, autoSave: true, cancellationToken: cancellationToken);
            }

            return;
        }

        message.MarkCompleted();
        await _outboxRepository.UpdateAsync(message, autoSave: true, cancellationToken: cancellationToken);
    }

    private async Task<List<WebhookSubscription>> GetMatchingSubscriptionsAsync(WebhookOutboxMessage message)
    {
        using (_dataFilter.Disable<IWebhookPartnerDataFilter>())
        {
            var queryable = await _subscriptionRepository.GetQueryableAsync();
            return queryable
                .Where(x => x.PartnerId == message.PartnerId && x.Status == WebhookSubscriptionStatus.Active)
                .ToList();
        }
    }

    private async Task LogDeliveryAsync(
        WebhookOutboxMessage message,
        WebhookSubscription subscription,
        int attemptNumber,
        WebhookDeliveryAttemptStatus status,
        int? httpStatusCode,
        string? responseBody,
        int durationMs,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        await _deliveryRepository.InsertAsync(
            new WebhookDelivery(
                _guidGenerator.Create(),
                message.Id,
                subscription.Id,
                message.PartnerId,
                attemptNumber,
                status,
                httpStatusCode,
                responseBody,
                durationMs,
                errorCode,
                Clock.Now),
            autoSave: true,
            cancellationToken: cancellationToken);
    }

    private async Task MoveToDeadLetterAsync(
        WebhookOutboxMessage message,
        string reason,
        CancellationToken cancellationToken)
    {
        message.MarkDeadLettered();
        await _outboxRepository.UpdateAsync(message, autoSave: true, cancellationToken: cancellationToken);

        var existing = await _deadLetterRepository.FirstOrDefaultAsync(x => x.OutboxMessageId == message.Id);
        if (existing == null)
        {
            await _deadLetterRepository.InsertAsync(
                new WebhookDeadLetter(
                    _guidGenerator.Create(),
                    message.Id,
                    message.PartnerId,
                    message.EventType,
                    message.PayloadJson,
                    reason,
                    Clock.Now),
                autoSave: true,
                cancellationToken: cancellationToken);
        }
    }
}
