using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;
using Zahy.Connectors;
using Zahy.OrderLedger;
using Zahy.Webhooks;

namespace Zahy.PlatformIntegration;

public class ConnectorAggregatorWebhookFlowTests : PlatformIntegrationTestBase
{
    private readonly IConnectorOrderIngestionService _connectorIngestionService;
    private readonly IWebhookSubscriptionAppService _subscriptionAppService;
    private readonly IWebhookDeliveryProcessor _deliveryProcessor;
    private readonly IRepository<OrderRecord, Guid> _orderRecordRepository;
    private readonly IRepository<WebhookOutboxMessage, Guid> _outboxRepository;
    private readonly InMemoryAggregatorConnectorTransport _aggregatorTransport;
    private readonly InMemoryWebhookDeliveryTransport _webhookTransport;
    private readonly TestCurrentPartner _currentPartner;
    private readonly IClock _clock;

    public ConnectorAggregatorWebhookFlowTests()
    {
        _connectorIngestionService = GetRequiredService<IConnectorOrderIngestionService>();
        _subscriptionAppService = GetRequiredService<IWebhookSubscriptionAppService>();
        _deliveryProcessor = GetRequiredService<IWebhookDeliveryProcessor>();
        _orderRecordRepository = GetRequiredService<IRepository<OrderRecord, Guid>>();
        _outboxRepository = GetRequiredService<IRepository<WebhookOutboxMessage, Guid>>();
        _aggregatorTransport = GetRequiredService<InMemoryAggregatorConnectorTransport>();
        _webhookTransport = GetRequiredService<InMemoryWebhookDeliveryTransport>();
        _currentPartner = GetRequiredService<TestCurrentPartner>();
        _clock = GetRequiredService<IClock>();
    }

    [Fact]
    public async Task Should_Deliver_Signed_Webhook_For_Mock_Aggregator_Order_End_To_End()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _currentPartner.Id = partnerId;
        _aggregatorTransport.Reset();
        _webhookTransport.Reset();

        _aggregatorTransport.EnqueueOrder(CreateInboundOrder(
            partnerId,
            tenantId,
            "e2e-aggregator-order",
            _clock.Now));

        string signingSecret = string.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var subscription = await _subscriptionAppService.CreateAsync(new CreateWebhookSubscriptionInput
            {
                TargetUrl = "https://fake.local/aggregator-orders",
                EventTypes =
                [
                    WebhookEventTypes.OrderCreated,
                    WebhookEventTypes.OrderPaid
                ]
            });

            signingSecret = subscription.SigningSecret;

            var ingest = await _connectorIngestionService.IngestReceivedOrderAsync(
                CreateConnectorContext(partnerId, tenantId),
                CreateReceiveRequest("e2e-aggregator-order"));

            ingest.IsNew.ShouldBeTrue();

            var ledger = await _orderRecordRepository.GetListAsync();
            ledger.Count.ShouldBe(1);
            ledger.Single().SourceSystem.ShouldBe("connector:aggregator:mock-aggregator");
            ledger.Single().SourceOrderId.ShouldBe("e2e-aggregator-order");

            var outbox = await _outboxRepository.GetListAsync();
            outbox.Any(x => x.EventType == WebhookEventTypes.OrderCreated).ShouldBeTrue();

            await _deliveryProcessor.ProcessPendingAsync();
        });

        _webhookTransport.Requests.Count.ShouldBeGreaterThanOrEqualTo(1);

        var createdDelivery = _webhookTransport.Requests
            .FirstOrDefault(x => x.EventType == WebhookEventTypes.OrderCreated);
        createdDelivery.ShouldNotBeNull();

        WebhookHmacSigner.Verify(
                signingSecret,
                createdDelivery!.PayloadJson,
                createdDelivery.UnixTimestamp,
                createdDelivery.Signature)
            .ShouldBeTrue();
    }

    private static AggregatorInboundOrder CreateInboundOrder(
        Guid partnerId,
        Guid tenantId,
        string externalOrderId,
        DateTime placedAt) =>
        new()
        {
            ExternalOrderId = externalOrderId,
            PartnerId = partnerId,
            TenantId = tenantId,
            OutletExternalId = "branch-001",
            PlacedAt = placedAt,
            AcceptDeadlineUtc = placedAt.AddMinutes(ConnectorConsts.DefaultAcceptWindowMinutes),
            AcceptWithinMinutes = ConnectorConsts.DefaultAcceptWindowMinutes,
            Subtotal = 90m,
            TaxAmount = 13.5m,
            DeliveryFee = 10m,
            TotalAmount = 113.5m,
            Lines =
            [
                new AggregatorInboundOrderLine
                {
                    LineNumber = 1,
                    Sku = "SKU-AGG-1",
                    ProductName = "Aggregator item",
                    Quantity = 1,
                    UnitPrice = 90m,
                    LineTotal = 90m
                }
            ]
        };

    private static ConnectorContext CreateConnectorContext(Guid partnerId, Guid tenantId) =>
        new()
        {
            PartnerId = partnerId,
            TenantId = tenantId,
            ConnectorCode = ConnectorConsts.MockAggregatorCode
        };

    private static ReceiveOrderRequest CreateReceiveRequest(string externalOrderId) =>
        new()
        {
            Intent = ReceiveOrderIntent.InboundFromPartner,
            ExternalOrderId = externalOrderId
        };
}
