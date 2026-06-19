using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Zahy.Identity.Permissions;

namespace Zahy.Webhooks;

[Authorize(ZahyPermissions.Webhooks.Manage)]
public class WebhookAdminAppService : ApplicationService, IWebhookAdminAppService
{
    private readonly IRepository<WebhookSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<WebhookDelivery, Guid> _deliveryRepository;
    private readonly IRepository<WebhookDeadLetter, Guid> _deadLetterRepository;
    private readonly IRepository<WebhookOutboxMessage, Guid> _outboxRepository;
    private readonly IWebhookOutboxPublisher _outboxPublisher;
    private readonly IWebhookDeliveryProcessor _deliveryProcessor;
    private readonly IDataFilter _dataFilter;

    public WebhookAdminAppService(
        IRepository<WebhookSubscription, Guid> subscriptionRepository,
        IRepository<WebhookDelivery, Guid> deliveryRepository,
        IRepository<WebhookDeadLetter, Guid> deadLetterRepository,
        IRepository<WebhookOutboxMessage, Guid> outboxRepository,
        IWebhookOutboxPublisher outboxPublisher,
        IWebhookDeliveryProcessor deliveryProcessor,
        IDataFilter dataFilter)
    {
        _subscriptionRepository = subscriptionRepository;
        _deliveryRepository = deliveryRepository;
        _deadLetterRepository = deadLetterRepository;
        _outboxRepository = outboxRepository;
        _outboxPublisher = outboxPublisher;
        _deliveryProcessor = deliveryProcessor;
        _dataFilter = dataFilter;
    }

    public virtual async Task<List<WebhookSubscriptionDto>> GetSubscriptionsAsync(Guid? partnerId = null)
    {
        using (_dataFilter.Disable<IWebhookPartnerDataFilter>())
        {
            var queryable = await _subscriptionRepository.GetQueryableAsync();
            if (partnerId.HasValue)
            {
                queryable = queryable.Where(x => x.PartnerId == partnerId.Value);
            }

            return queryable
                .OrderByDescending(x => x.CreationTime)
                .Select(x => WebhookDtoMapper.ToDto(x))
                .ToList();
        }
    }

    public virtual async Task<PagedResultDto<WebhookDeliveryDto>> GetDeliveriesAsync(
        PagedResultRequestDto input,
        Guid? partnerId = null)
    {
        using (_dataFilter.Disable<IWebhookPartnerDataFilter>())
        {
            var queryable = await _deliveryRepository.GetQueryableAsync();
            if (partnerId.HasValue)
            {
                queryable = queryable.Where(x => x.PartnerId == partnerId.Value);
            }

            var total = queryable.Count();
            var items = queryable
                .OrderByDescending(x => x.AttemptedAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .Select(x => WebhookDtoMapper.ToDto(x))
                .ToList();

            return new PagedResultDto<WebhookDeliveryDto>(total, items);
        }
    }

    public virtual async Task<PagedResultDto<WebhookDeadLetterDto>> GetDeadLettersAsync(
        PagedResultRequestDto input,
        Guid? partnerId = null)
    {
        using (_dataFilter.Disable<IWebhookPartnerDataFilter>())
        {
            var queryable = await _deadLetterRepository.GetQueryableAsync();
            if (partnerId.HasValue)
            {
                queryable = queryable.Where(x => x.PartnerId == partnerId.Value);
            }

            var total = queryable.Count();
            var items = queryable
                .OrderByDescending(x => x.FinalAttemptAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .Select(x => WebhookDtoMapper.ToDto(x))
                .ToList();

            return new PagedResultDto<WebhookDeadLetterDto>(total, items);
        }
    }

    [UnitOfWork]
    public virtual async Task ReplayDeadLetterAsync(Guid deadLetterId)
    {
        WebhookDeadLetter deadLetter;
        WebhookOutboxMessage outbox;

        using (_dataFilter.Disable<IWebhookPartnerDataFilter>())
        {
            deadLetter = await _deadLetterRepository.FindAsync(deadLetterId)
                ?? throw new BusinessException(WebhookErrorCodes.DeadLetterNotFound).WithData("Id", deadLetterId);

            outbox = await _outboxRepository.FindAsync(deadLetter.OutboxMessageId)
                ?? throw new BusinessException(WebhookErrorCodes.OutboxNotFound)
                    .WithData("Id", deadLetter.OutboxMessageId);
        }

        var replayKey = $"{outbox.IdempotencyKey}:replay:{GuidGenerator.Create():N}";
        await _outboxPublisher.EnqueueAsync(new WebhookEnqueueRequest
        {
            PartnerId = outbox.PartnerId,
            EventType = outbox.EventType,
            IdempotencyKey = replayKey,
            PayloadJson = outbox.PayloadJson
        });

        deadLetter.MarkReplayed(Clock.Now);
        await _deadLetterRepository.UpdateAsync(deadLetter, autoSave: true);
        await _deliveryProcessor.ProcessPendingAsync();
    }
}
