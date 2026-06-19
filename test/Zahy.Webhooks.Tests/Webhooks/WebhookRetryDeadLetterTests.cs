using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Zahy.Webhooks;

public class WebhookRetryDeadLetterTests : ZahyWebhooksTestBase
{
    private readonly IWebhookSubscriptionAppService _subscriptionAppService;
    private readonly IWebhookOutboxPublisher _publisher;
    private readonly IWebhookDeliveryProcessor _processor;
    private readonly IRepository<WebhookOutboxMessage, Guid> _outboxRepository;
    private readonly IRepository<WebhookDeadLetter, Guid> _deadLetterRepository;
    private readonly InMemoryWebhookDeliveryTransport _transport;
    private readonly TestCurrentPartner _currentPartner;

    public WebhookRetryDeadLetterTests()
    {
        _subscriptionAppService = GetRequiredService<IWebhookSubscriptionAppService>();
        _publisher = GetRequiredService<IWebhookOutboxPublisher>();
        _processor = GetRequiredService<IWebhookDeliveryProcessor>();
        _outboxRepository = GetRequiredService<IRepository<WebhookOutboxMessage, Guid>>();
        _deadLetterRepository = GetRequiredService<IRepository<WebhookDeadLetter, Guid>>();
        _transport = GetRequiredService<InMemoryWebhookDeliveryTransport>();
        _currentPartner = GetRequiredService<TestCurrentPartner>();
    }

    [Fact]
    public async Task Should_Move_Message_To_Dead_Letter_After_Max_Retries()
    {
        var partnerId = Guid.NewGuid();
        _currentPartner.Id = partnerId;
        _transport.Reset();
        _transport.ConfigureHandler(_ => new WebhookTransportResult
        {
            Success = false,
            IsRetryable = true,
            HttpStatusCode = 500,
            ResponseBody = "fail",
            DurationMs = 1
        });

        Guid outboxId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            await _subscriptionAppService.CreateAsync(new CreateWebhookSubscriptionInput
            {
                TargetUrl = "https://fake.local/webhook",
                EventTypes = [WebhookEventTypes.OrderPaid]
            });

            outboxId = await _publisher.EnqueueAsync(new WebhookEnqueueRequest
            {
                PartnerId = partnerId,
                EventType = WebhookEventTypes.OrderPaid,
                IdempotencyKey = "order.paid:retry-test:1",
                PayloadJson = """{"orderId":"retry-test","totalAmount":10}"""
            });
        });

        for (var attempt = 0; attempt < WebhookConsts.DefaultMaxDeliveryAttempts + 1; attempt++)
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var message = await _outboxRepository.GetAsync(outboxId);
                if (message.Status == WebhookOutboxStatus.DeadLettered)
                {
                    return;
                }

                message.MarkDueNow();
                await _outboxRepository.UpdateAsync(message, autoSave: true);
                await _processor.ProcessPendingAsync();
            });
        }

        await WithUnitOfWorkAsync(async () =>
        {
            var message = await _outboxRepository.GetAsync(outboxId);
            message.Status.ShouldBe(WebhookOutboxStatus.DeadLettered);

            var deadLetters = await _deadLetterRepository.GetListAsync(x => x.OutboxMessageId == outboxId);
            deadLetters.Count.ShouldBe(1);
            deadLetters.Single().Reason.ShouldBe("MaxRetriesExceeded");
        });
    }
}
