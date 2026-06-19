using Shouldly;
using Volo.Abp.Timing;
using Xunit;

namespace Zahy.Connectors;

public class MockAggregatorConnectorTests : ZahyConnectorsTestBase
{
    private readonly MockAggregatorConnector _connector;
    private readonly InMemoryAggregatorConnectorTransport _transport;
    private readonly IClock _clock;

    public MockAggregatorConnectorTests()
    {
        _connector = GetRequiredService<MockAggregatorConnector>();
        _transport = GetRequiredService<InMemoryAggregatorConnectorTransport>();
        _clock = GetRequiredService<IClock>();
    }

    [Fact]
    public async Task Should_Map_Inbound_Transport_Order_To_Canonical_Order()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.EnqueueOrder(AggregatorTestData.CreateInboundOrder(
            partnerId, tenantId, "agg-order-1", _clock.Now));

        var result = await _connector.ReceiveOrderAsync(
            AggregatorTestData.CreateContext(partnerId, tenantId),
            AggregatorTestData.CreateReceiveRequest("agg-order-1"));

        result.Success.ShouldBeTrue();
        result.Value!.ExternalOrderId.ShouldBe("agg-order-1");
        result.Value.Status.ShouldBe(CanonicalOrderStatus.PendingAcceptance);
        result.Value.AcceptDeadlineUtc.ShouldNotBeNull();
        result.Value.ConnectorKind.ShouldBe(ConnectorKind.Aggregator);
    }

    [Fact]
    public async Task Should_Sync_Menu_From_Fake_Transport()
    {
        _transport.Reset();
        _transport.SetMenuItems([
            new AggregatorTransportMenuItem
            {
                ExternalItemId = "menu-1",
                NameEn = "Shawarma",
                Price = 25m,
                OutletExternalId = "branch-001"
            }
        ]);

        var result = await _connector.SyncMenuAsync(
            AggregatorTestData.CreateContext(Guid.NewGuid(), Guid.NewGuid()),
            new CanonicalMenuSyncRequest { OutletExternalId = "branch-001" });

        result.Success.ShouldBeTrue();
        result.Value!.Items.Count.ShouldBe(1);
        result.Value.Items[0].NameEn.ShouldBe("Shawarma");
    }

    [Fact]
    public async Task Should_Accept_Pending_Aggregator_Order()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.EnqueueOrder(AggregatorTestData.CreateInboundOrder(
            partnerId, tenantId, "accept-me", _clock.Now));

        var result = await _connector.AcceptOrderAsync(
            AggregatorTestData.CreateContext(partnerId, tenantId),
            new OrderActionRequest { ExternalOrderId = "accept-me" });

        result.Success.ShouldBeTrue();
        result.Value!.Outcome.ShouldBe(CanonicalAcceptOutcome.Accepted);
        _transport.TryGetOrder("accept-me", out var updated).ShouldBeTrue();
        updated!.Status.ShouldBe(AggregatorInboundOrderStatus.Accepted);
    }

    [Fact]
    public async Task Should_Reject_Pending_Aggregator_Order()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.EnqueueOrder(AggregatorTestData.CreateInboundOrder(
            partnerId, tenantId, "reject-me", _clock.Now));

        var result = await _connector.RejectOrderAsync(
            AggregatorTestData.CreateContext(partnerId, tenantId),
            new OrderActionRequest { ExternalOrderId = "reject-me", Reason = "Out of stock" });

        result.Success.ShouldBeTrue();
        result.Value!.Outcome.ShouldBe(CanonicalAcceptOutcome.Rejected);
    }

    [Fact]
    public async Task Should_Return_Expired_When_Accepting_Past_Accept_Deadline()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.EnqueueOrder(AggregatorTestData.CreateInboundOrder(
            partnerId,
            tenantId,
            "expired-order",
            _clock.Now,
            acceptDeadlineUtc: _clock.Now.AddMinutes(-1)));

        var result = await _connector.AcceptOrderAsync(
            AggregatorTestData.CreateContext(partnerId, tenantId),
            new OrderActionRequest { ExternalOrderId = "expired-order" });

        result.Success.ShouldBeTrue();
        result.Value!.Outcome.ShouldBe(CanonicalAcceptOutcome.Expired);
        _transport.TryGetOrder("expired-order", out var order).ShouldBeTrue();
        order!.Status.ShouldBe(AggregatorInboundOrderStatus.New);
    }
}
