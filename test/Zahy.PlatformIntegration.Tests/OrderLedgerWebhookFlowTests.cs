using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.OrderLedger;
using Zahy.Webhooks;

namespace Zahy.PlatformIntegration;

public class OrderLedgerWebhookFlowTests : PlatformIntegrationTestBase
{
    private readonly IOrderLedgerIngestionService _ingestionService;
    private readonly IWebhookSubscriptionAppService _subscriptionAppService;
    private readonly IWebhookDeliveryProcessor _deliveryProcessor;
    private readonly IRepository<WebhookOutboxMessage, Guid> _outboxRepository;
    private readonly InMemoryWebhookDeliveryTransport _transport;
    private readonly TestCurrentPartner _currentPartner;

    public OrderLedgerWebhookFlowTests()
    {
        _ingestionService = GetRequiredService<IOrderLedgerIngestionService>();
        _subscriptionAppService = GetRequiredService<IWebhookSubscriptionAppService>();
        _deliveryProcessor = GetRequiredService<IWebhookDeliveryProcessor>();
        _outboxRepository = GetRequiredService<IRepository<WebhookOutboxMessage, Guid>>();
        _transport = GetRequiredService<InMemoryWebhookDeliveryTransport>();
        _currentPartner = GetRequiredService<TestCurrentPartner>();
    }

    [Fact]
    public async Task Should_Enqueue_Exactly_One_OrderCreated_On_New_Ingest()
    {
        var partnerId = Guid.NewGuid();
        _currentPartner.Id = partnerId;

        await WithUnitOfWorkAsync(async () =>
        {
            var ingest = await _ingestionService.IngestSnapshotAsync(CreateSnapshot(
                partnerId,
                sourceOrderId: "new-order-1",
                version: 1,
                OrderStatus.Created,
                PaymentStatus.Unpaid));

            ingest.IsNew.ShouldBeTrue();

            var outbox = await _outboxRepository.GetListAsync();
            outbox.Count(x => x.EventType == WebhookEventTypes.OrderCreated).ShouldBe(1);
            outbox.Count.ShouldBe(1);
        });
    }

    [Fact]
    public async Task Duplicate_Ingest_Does_Not_Refire_Webhook()
    {
        var partnerId = Guid.NewGuid();
        _currentPartner.Id = partnerId;

        var snapshot = CreateSnapshot(
            partnerId,
            sourceOrderId: "duplicate-order",
            version: 1,
            OrderStatus.Created,
            PaymentStatus.Unpaid);

        await WithUnitOfWorkAsync(async () =>
        {
            var first = await _ingestionService.IngestSnapshotAsync(snapshot);
            first.IsNew.ShouldBeTrue();

            var outboxAfterFirst = await _outboxRepository.GetListAsync();
            var countAfterFirst = outboxAfterFirst.Count;
            countAfterFirst.ShouldBeGreaterThan(0);

            var second = await _ingestionService.IngestSnapshotAsync(snapshot);
            second.IsNew.ShouldBeFalse();

            var outboxAfterSecond = await _outboxRepository.GetListAsync();
            outboxAfterSecond.Count.ShouldBe(countAfterFirst);
        });
    }

    [Fact]
    public async Task Should_Enqueue_OrderStatusChanged_Once_When_New_SourceVersion_Changes_Status()
    {
        var partnerId = Guid.NewGuid();
        _currentPartner.Id = partnerId;

        await WithUnitOfWorkAsync(async () =>
        {
            await _ingestionService.IngestSnapshotAsync(CreateSnapshot(
                partnerId,
                sourceOrderId: "status-order",
                version: 1,
                OrderStatus.Created,
                PaymentStatus.Unpaid));

            await _ingestionService.IngestSnapshotAsync(CreateSnapshot(
                partnerId,
                sourceOrderId: "status-order",
                version: 2,
                OrderStatus.Paid,
                PaymentStatus.Paid));

            var outbox = await _outboxRepository.GetListAsync(x => x.EventType == WebhookEventTypes.OrderStatusChanged);
            outbox.Count.ShouldBe(1);
        });
    }

    [Fact]
    public async Task Should_Deliver_Signed_Webhook_End_To_End_Via_Fake_Transport()
    {
        var partnerId = Guid.NewGuid();
        _currentPartner.Id = partnerId;
        _transport.Reset();

        string signingSecret = string.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var subscription = await _subscriptionAppService.CreateAsync(new CreateWebhookSubscriptionInput
            {
                TargetUrl = "https://fake.local/orders",
                EventTypes =
                [
                    WebhookEventTypes.OrderCreated,
                    WebhookEventTypes.OrderPaid
                ]
            });

            signingSecret = subscription.SigningSecret;

            var ingest = await _ingestionService.IngestSnapshotAsync(CreateSnapshot(
                partnerId,
                sourceOrderId: "e2e-order",
                version: 1,
                OrderStatus.Paid,
                PaymentStatus.Paid,
                totalAmount: 199.50m));

            ingest.IsNew.ShouldBeTrue();

            var outbox = await _outboxRepository.GetListAsync();
            outbox.Any(x => x.EventType == WebhookEventTypes.OrderCreated).ShouldBeTrue();
            outbox.Any(x => x.EventType == WebhookEventTypes.OrderPaid).ShouldBeTrue();

            await _deliveryProcessor.ProcessPendingAsync();
        });

        _transport.Requests.Count.ShouldBeGreaterThanOrEqualTo(2);

        var paidDelivery = _transport.Requests
            .LastOrDefault(x => x.EventType == WebhookEventTypes.OrderPaid);
        paidDelivery.ShouldNotBeNull();

        WebhookHmacSigner.Verify(
                signingSecret,
                paidDelivery!.PayloadJson,
                paidDelivery.UnixTimestamp,
                paidDelivery.Signature)
            .ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Deliver_Order_Webhooks_Only_To_Entitled_Partner_Subscription()
    {
        var orderPartnerId = Guid.NewGuid();
        var otherPartnerId = Guid.NewGuid();
        _transport.Reset();

        await WithUnitOfWorkAsync(async () =>
        {
            _currentPartner.Id = otherPartnerId;
            await _subscriptionAppService.CreateAsync(new CreateWebhookSubscriptionInput
            {
                TargetUrl = "https://fake.local/other-partner",
                EventTypes = [WebhookEventTypes.OrderCreated]
            });

            _currentPartner.Id = null;
            await _ingestionService.IngestSnapshotAsync(CreateSnapshot(
                orderPartnerId,
                sourceOrderId: "scoped-order",
                version: 1,
                OrderStatus.Created,
                PaymentStatus.Unpaid));

            await _deliveryProcessor.ProcessPendingAsync();
        });

        _transport.Requests.ShouldBeEmpty();
    }

    private static OrderSourceSnapshot CreateSnapshot(
        Guid partnerId,
        string sourceOrderId,
        long version,
        OrderStatus status,
        PaymentStatus paymentStatus,
        decimal totalAmount = 75m) =>
        new()
        {
            SourceSystem = OrderLedgerConsts.FakeSourceSystem,
            SourceOrderId = sourceOrderId,
            SourceVersion = version,
            TenantId = Guid.NewGuid(),
            PartnerId = partnerId,
            Direction = OrderDirection.Outbound,
            Status = status,
            PaymentStatus = paymentStatus,
            TotalAmount = totalAmount,
            SourceTimestamp = DateTime.UtcNow.AddMinutes(version),
            Lines =
            [
                new OrderLineSnapshot
                {
                    LineNumber = 1,
                    Sku = "SKU-1",
                    ProductName = "Item",
                    Quantity = 1,
                    UnitPrice = totalAmount,
                    LineTotal = totalAmount
                }
            ]
        };
}
