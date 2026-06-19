using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Zahy.Webhooks;

public class WebhookOutboxIdempotencyTests : ZahyWebhooksTestBase
{
    private readonly IWebhookOutboxPublisher _publisher;
    private readonly IRepository<WebhookOutboxMessage, Guid> _outboxRepository;

    public WebhookOutboxIdempotencyTests()
    {
        _publisher = GetRequiredService<IWebhookOutboxPublisher>();
        _outboxRepository = GetRequiredService<IRepository<WebhookOutboxMessage, Guid>>();
    }

    [Fact]
    public async Task Should_Return_Same_Outbox_Id_For_Duplicate_Idempotency_Key()
    {
        var partnerId = Guid.NewGuid();
        var request = new WebhookEnqueueRequest
        {
            PartnerId = partnerId,
            EventType = WebhookEventTypes.OrderCreated,
            IdempotencyKey = "order.created:abc:1",
            PayloadJson = """{"orderId":"abc"}"""
        };

        Guid firstId = Guid.Empty;
        Guid secondId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            firstId = await _publisher.EnqueueAsync(request);
            secondId = await _publisher.EnqueueAsync(request);

            firstId.ShouldBe(secondId);
            var count = (await _outboxRepository.GetListAsync()).Count(x => x.IdempotencyKey == request.IdempotencyKey);
            count.ShouldBe(1);
        });
    }
}
