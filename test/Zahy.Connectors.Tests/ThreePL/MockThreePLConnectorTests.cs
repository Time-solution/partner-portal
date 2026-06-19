using Shouldly;
using Xunit;

namespace Zahy.Connectors;

public class MockThreePLConnectorTests : ZahyConnectorsTestBase
{
    private readonly IThreePLConnector _connector;
    private readonly InMemoryThreePLConnectorTransport _transport;

    public MockThreePLConnectorTests()
    {
        _connector = GetRequiredService<MockThreePLConnector>();
        _transport = GetRequiredService<InMemoryThreePLConnectorTransport>();
    }

    [Fact]
    public async Task Should_Push_Fulfillment_Order_To_ThreePL_Transport()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.StageOrder(ThreePLTestData.CreateFulfillmentOrder(partnerId, tenantId, "3pl-order-1"));

        var result = await _connector.ReceiveOrderAsync(
            ThreePLTestData.CreateContext(partnerId, tenantId),
            new ReceiveOrderRequest
            {
                Intent = ReceiveOrderIntent.OutboundToPartner,
                ExternalOrderId = "3pl-order-1"
            });

        result.Success.ShouldBeTrue();
        result.Value!.Direction.ShouldBe(CanonicalOrderDirection.Outbound);
        result.Value.ConnectorKind.ShouldBe(ConnectorKind.ThreePL);
        _transport.TryGetOrder("3pl-order-1", out var stored).ShouldBeTrue();
        stored!.Status.ShouldBe(ThreePLFulfillmentStatus.Pushed);
    }

    [Fact]
    public async Task Should_Update_ThreePL_Order_Status()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.StageOrder(ThreePLTestData.CreateFulfillmentOrder(partnerId, tenantId, "3pl-status-1"));
        _transport.MarkPushed("3pl-status-1");

        var result = await _connector.UpdateStatusAsync(
            ThreePLTestData.CreateContext(partnerId, tenantId),
            new CanonicalOrderStatusUpdate
            {
                ExternalOrderId = "3pl-status-1",
                Version = 2,
                NewStatus = CanonicalOrderStatus.InTransit,
                OccurredAt = DateTime.UtcNow
            });

        result.Success.ShouldBeTrue();
        _transport.TryGetOrder("3pl-status-1", out var order).ShouldBeTrue();
        order!.Status.ShouldBe(ThreePLFulfillmentStatus.Shipped);
        order.TrackingNumber.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_Get_ThreePL_Tracking()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.StageOrder(ThreePLTestData.CreateFulfillmentOrder(partnerId, tenantId, "3pl-track-1"));
        _transport.UpdateStatus("3pl-track-1", ThreePLFulfillmentStatus.Shipped, "3PL-TRK-999");

        var result = await _connector.GetTrackingAsync(
            ThreePLTestData.CreateContext(partnerId, tenantId),
            new OrderActionRequest { ExternalOrderId = "3pl-track-1" });

        result.Success.ShouldBeTrue();
        result.Value!.TrackingNumber.ShouldBe("3PL-TRK-999");
        result.Value.Status.ShouldBe(CanonicalOrderStatus.InTransit);
    }

    [Fact]
    public async Task Should_Handle_ThreePL_Return()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.StageOrder(ThreePLTestData.CreateFulfillmentOrder(partnerId, tenantId, "3pl-return-1"));
        _transport.MarkPushed("3pl-return-1");

        var result = await _connector.HandleReturnAsync(
            ThreePLTestData.CreateContext(partnerId, tenantId),
            new CanonicalReturnRequest
            {
                ExternalOrderId = "3pl-return-1",
                Reason = "Damaged on delivery"
            });

        result.Success.ShouldBeTrue();
        _transport.GetReturns().Count.ShouldBe(1);
        _transport.TryGetOrder("3pl-return-1", out var order).ShouldBeTrue();
        order!.Status.ShouldBe(ThreePLFulfillmentStatus.Returned);
    }

    [Fact]
    public async Task Should_Reconcile_ThreePL_Inventory()
    {
        _transport.Reset();
        _transport.SetExpectedInventory([
            new ThreePLInventoryRecord { Sku = "SKU-A", ExpectedQuantity = 10 },
            new ThreePLInventoryRecord { Sku = "SKU-B", ExpectedQuantity = 5 }
        ]);

        var result = await _connector.ReconcileInventoryAsync(
            ThreePLTestData.CreateContext(Guid.NewGuid(), Guid.NewGuid()),
            new CanonicalInventoryReconcileRequest
            {
                WarehouseExternalId = "wh-riyadh-1",
                ReportedOnHand =
                [
                    new CanonicalInventoryReconcileLine { Sku = "SKU-A", ReportedQuantity = 8, ExpectedQuantity = 0, Delta = 0 },
                    new CanonicalInventoryReconcileLine { Sku = "SKU-B", ReportedQuantity = 5, ExpectedQuantity = 0, Delta = 0 }
                ]
            });

        result.Success.ShouldBeTrue();
        result.Value!.Lines.Count.ShouldBe(2);
        result.Value.Lines.First(x => x.Sku == "SKU-A").Delta.ShouldBe(-2);
        result.Value.Lines.First(x => x.Sku == "SKU-B").Delta.ShouldBe(0);
    }
}
