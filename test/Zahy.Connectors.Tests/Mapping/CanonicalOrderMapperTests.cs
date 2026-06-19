using Shouldly;
using Xunit;
using Zahy.OrderLedger;

namespace Zahy.Connectors;

public class CanonicalOrderMapperTests : ZahyConnectorsTestBase
{
    private readonly ICanonicalOrderMapper _mapper;

    public CanonicalOrderMapperTests()
    {
        _mapper = GetRequiredService<ICanonicalOrderMapper>();
    }

    [Fact]
    public void Should_Round_Trip_Canonical_Order_Through_Ledger_Snapshot()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var placedAt = DateTime.UtcNow.AddMinutes(-5);

        var original = new CanonicalOrder
        {
            ExternalOrderId = "ord-round-trip-1",
            Version = 2,
            ConnectorCode = ConnectorConsts.MockAggregatorCode,
            ConnectorKind = ConnectorKind.Aggregator,
            PartnerId = partnerId,
            TenantId = tenantId,
            Direction = CanonicalOrderDirection.Inbound,
            Status = CanonicalOrderStatus.PendingAcceptance,
            PaymentState = CanonicalPaymentState.Unpaid,
            Currency = ConnectorConsts.DefaultCurrency,
            Subtotal = 80m,
            TaxAmount = 12m,
            DeliveryFee = 8m,
            TotalAmount = 100m,
            PlacedAt = placedAt,
            Lines =
            [
                new CanonicalOrderLine
                {
                    LineNumber = 1,
                    Sku = "SKU-1",
                    ProductName = "Test item",
                    Quantity = 2,
                    UnitPrice = 40m,
                    LineTotal = 80m
                }
            ]
        };

        var snapshot = _mapper.ToSnapshot(original);
        snapshot.SourceSystem.ShouldBe("connector:aggregator:mock-aggregator");
        snapshot.SourceOrderId.ShouldBe(original.ExternalOrderId);
        snapshot.SourceVersion.ShouldBe(original.Version);
        snapshot.PartnerId.ShouldBe(partnerId);
        snapshot.TenantId.ShouldBe(tenantId);
        snapshot.Direction.ShouldBe(OrderDirection.Inbound);
        snapshot.Status.ShouldBe(OrderStatus.Created);
        snapshot.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);
        snapshot.TotalAmount.ShouldBe(100m);
        snapshot.Lines.Count.ShouldBe(1);

        var roundTripped = _mapper.FromSnapshot(
            snapshot,
            ConnectorKind.Aggregator,
            ConnectorConsts.MockAggregatorCode);

        roundTripped.ExternalOrderId.ShouldBe(original.ExternalOrderId);
        roundTripped.Version.ShouldBe(original.Version);
        roundTripped.ConnectorCode.ShouldBe(original.ConnectorCode);
        roundTripped.ConnectorKind.ShouldBe(original.ConnectorKind);
        roundTripped.PartnerId.ShouldBe(original.PartnerId);
        roundTripped.TenantId.ShouldBe(original.TenantId);
        roundTripped.Direction.ShouldBe(original.Direction);
        roundTripped.PaymentState.ShouldBe(original.PaymentState);
        roundTripped.TotalAmount.ShouldBe(original.TotalAmount);
        roundTripped.PlacedAt.ShouldBe(original.PlacedAt);
        roundTripped.Lines.Count.ShouldBe(1);
        roundTripped.Lines[0].Sku.ShouldBe("SKU-1");
    }

    [Fact]
    public void Should_Map_InTransit_With_Unpaid_Payment_To_Non_Paid_Ledger_Record()
    {
        var snapshot = _mapper.ToSnapshot(new CanonicalOrder
        {
            ExternalOrderId = "ord-in-transit-unpaid",
            Version = 1,
            ConnectorCode = ConnectorConsts.MockCarrierCode,
            ConnectorKind = ConnectorKind.Carrier,
            PartnerId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = CanonicalOrderStatus.InTransit,
            PaymentState = CanonicalPaymentState.Unpaid,
            TotalAmount = 50m,
            PlacedAt = DateTime.UtcNow
        });

        snapshot.Status.ShouldBe(OrderStatus.Paid);
        snapshot.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);
        snapshot.PaymentStatus.ShouldNotBe(PaymentStatus.Paid);
    }

    [Fact]
    public void Should_Map_Delivered_Status_To_Fulfilled_Ledger_Status()
    {
        var snapshot = _mapper.ToSnapshot(new CanonicalOrder
        {
            ExternalOrderId = "ord-delivered",
            Version = 1,
            ConnectorCode = ConnectorConsts.MockCarrierCode,
            ConnectorKind = ConnectorKind.Carrier,
            PartnerId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = CanonicalOrderStatus.Delivered,
            PaymentState = CanonicalPaymentState.Paid,
            TotalAmount = 50m,
            PlacedAt = DateTime.UtcNow
        });

        snapshot.Status.ShouldBe(OrderStatus.Fulfilled);
        snapshot.PaymentStatus.ShouldBe(PaymentStatus.Paid);
    }
}
