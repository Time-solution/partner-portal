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

public class WebhookOutboxPublisher : ApplicationService, IWebhookOutboxPublisher
{
    private readonly IRepository<WebhookOutboxMessage, Guid> _outboxRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IDataFilter _dataFilter;

    public WebhookOutboxPublisher(
        IRepository<WebhookOutboxMessage, Guid> outboxRepository,
        IGuidGenerator guidGenerator,
        IDataFilter dataFilter)
    {
        _outboxRepository = outboxRepository;
        _guidGenerator = guidGenerator;
        _dataFilter = dataFilter;
    }

    [UnitOfWork]
    public virtual async Task<Guid> EnqueueAsync(
        WebhookEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        WebhookOutboxMessage? existing;
        using (_dataFilter.Disable<IWebhookPartnerDataFilter>())
        {
            var queryable = await _outboxRepository.GetQueryableAsync();
            existing = queryable.FirstOrDefault(x => x.IdempotencyKey == request.IdempotencyKey);
        }

        if (existing != null)
        {
            return existing.Id;
        }

        var message = new WebhookOutboxMessage(
            _guidGenerator.Create(),
            request.PartnerId,
            request.EventType,
            request.IdempotencyKey,
            request.PayloadJson,
            Clock.Now);

        await _outboxRepository.InsertAsync(message, autoSave: true, cancellationToken: cancellationToken);
        return message.Id;
    }
}
