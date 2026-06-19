using Shouldly;
using Xunit;

namespace Zahy.Connectors;

public class MockCarrierConnectorTests : ZahyConnectorsTestBase
{
    private readonly ICarrierConnector _connector;
    private readonly InMemoryCarrierConnectorTransport _transport;

    public MockCarrierConnectorTests()
    {
        _connector = GetRequiredService<MockCarrierConnector>();
        _transport = GetRequiredService<InMemoryCarrierConnectorTransport>();
    }

    [Fact]
    public async Task Should_Get_Carrier_Shipment_Rate()
    {
        var result = await _connector.GetRateAsync(
            CarrierTestData.CreateContext(Guid.NewGuid(), Guid.NewGuid()),
            new CarrierRateRequest
            {
                ExternalOrderId = "ship-rate-1",
                WeightKg = 2m,
                DestinationCity = "Jeddah"
            });

        result.Success.ShouldBeTrue();
        result.Value!.Amount.ShouldBeGreaterThan(0);
        result.Value.Currency.ShouldBe(ConnectorConsts.DefaultCurrency);
    }

    [Fact]
    public async Task Should_Create_Shipment_And_Label_Via_Carrier_Connector()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.StageShipment(CarrierTestData.CreateShipmentRequest(partnerId, tenantId, "ship-1"));

        var createResult = await _connector.ReceiveOrderAsync(
            CarrierTestData.CreateContext(partnerId, tenantId),
            new ReceiveOrderRequest
            {
                Intent = ReceiveOrderIntent.CreateShipment,
                ExternalOrderId = "ship-1"
            });

        createResult.Success.ShouldBeTrue();
        createResult.Value!.Status.ShouldBe(CanonicalOrderStatus.ReadyForHandoff);

        var labelResult = await _connector.CreateLabelAsync(
            CarrierTestData.CreateContext(partnerId, tenantId),
            new CarrierLabelRequest { ExternalOrderId = "ship-1", ServiceLevel = "Standard" });

        labelResult.Success.ShouldBeTrue();
        labelResult.Value!.TrackingNumber.ShouldStartWith("CR-");
        labelResult.Value.LabelReference.ShouldStartWith("LBL-");
    }

    [Fact]
    public async Task Should_Get_Carrier_Tracking()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.StageShipment(CarrierTestData.CreateShipmentRequest(partnerId, tenantId, "ship-track-1"));

        await _connector.ReceiveOrderAsync(
            CarrierTestData.CreateContext(partnerId, tenantId),
            new ReceiveOrderRequest
            {
                Intent = ReceiveOrderIntent.CreateShipment,
                ExternalOrderId = "ship-track-1"
            });

        await _connector.UpdateStatusAsync(
            CarrierTestData.CreateContext(partnerId, tenantId),
            new CanonicalOrderStatusUpdate
            {
                ExternalOrderId = "ship-track-1",
                Version = 2,
                NewStatus = CanonicalOrderStatus.InTransit,
                OccurredAt = DateTime.UtcNow
            });

        var tracking = await _connector.GetTrackingAsync(
            CarrierTestData.CreateContext(partnerId, tenantId),
            new OrderActionRequest { ExternalOrderId = "ship-track-1" });

        tracking.Success.ShouldBeTrue();
        tracking.Value!.Status.ShouldBe(CanonicalOrderStatus.InTransit);
        tracking.Value.TrackingNumber.ShouldNotBeNullOrWhiteSpace();
    }
}
