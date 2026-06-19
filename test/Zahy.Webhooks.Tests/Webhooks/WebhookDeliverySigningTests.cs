using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Zahy.Webhooks;

public class WebhookDeliverySigningTests : ZahyWebhooksTestBase
{
    private readonly IWebhookSubscriptionAppService _subscriptionAppService;
    private readonly IWebhookOutboxPublisher _publisher;
    private readonly IWebhookDeliveryProcessor _processor;
    private readonly InMemoryWebhookDeliveryTransport _transport;
    private readonly TestCurrentPartner _currentPartner;

    public WebhookDeliverySigningTests()
    {
        _subscriptionAppService = GetRequiredService<IWebhookSubscriptionAppService>();
        _publisher = GetRequiredService<IWebhookOutboxPublisher>();
        _processor = GetRequiredService<IWebhookDeliveryProcessor>();
        _transport = GetRequiredService<InMemoryWebhookDeliveryTransport>();
        _currentPartner = GetRequiredService<TestCurrentPartner>();
    }

    [Fact]
    public async Task Should_Deliver_Signed_Payload_To_InMemory_Transport()
    {
        var partnerId = Guid.NewGuid();
        _currentPartner.Id = partnerId;
        _transport.Reset();

        const string payload = """{"orderId":"signed-1","totalAmount":25}""";
        string capturedSecret = string.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var created = await _subscriptionAppService.CreateAsync(new CreateWebhookSubscriptionInput
            {
                TargetUrl = "https://fake.local/hook",
                EventTypes = [WebhookEventTypes.OrderCreated]
            });

            capturedSecret = created.SigningSecret;

            await _publisher.EnqueueAsync(new WebhookEnqueueRequest
            {
                PartnerId = partnerId,
                EventType = WebhookEventTypes.OrderCreated,
                IdempotencyKey = "order.created:signed-1:1",
                PayloadJson = payload
            });

            await _processor.ProcessPendingAsync();
        });

        _transport.Requests.Count.ShouldBe(1);
        var request = _transport.Requests.Single();
        WebhookHmacSigner.Verify(capturedSecret, payload, request.UnixTimestamp, request.Signature)
            .ShouldBeTrue();
    }
}
