namespace Zahy.Connectors;

internal static class AggregatorTestData
{
    public static AggregatorInboundOrder CreateInboundOrder(
        Guid partnerId,
        Guid tenantId,
        string externalOrderId,
        DateTime placedAt,
        DateTime? acceptDeadlineUtc = null) =>
        new()
        {
            ExternalOrderId = externalOrderId,
            PartnerId = partnerId,
            TenantId = tenantId,
            OutletExternalId = "branch-001",
            PlacedAt = placedAt,
            AcceptDeadlineUtc = acceptDeadlineUtc,
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

    public static ConnectorContext CreateContext(Guid partnerId, Guid tenantId) =>
        new()
        {
            PartnerId = partnerId,
            TenantId = tenantId,
            ConnectorCode = ConnectorConsts.MockAggregatorCode
        };

    public static ReceiveOrderRequest CreateReceiveRequest(string externalOrderId) =>
        new()
        {
            Intent = ReceiveOrderIntent.InboundFromPartner,
            ExternalOrderId = externalOrderId
        };
}
