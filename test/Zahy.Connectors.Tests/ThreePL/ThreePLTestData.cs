namespace Zahy.Connectors;

internal static class ThreePLTestData
{
    public static ThreePLFulfillmentOrder CreateFulfillmentOrder(
        Guid partnerId,
        Guid tenantId,
        string externalOrderId) =>
        new()
        {
            ExternalOrderId = externalOrderId,
            PartnerId = partnerId,
            TenantId = tenantId,
            WarehouseExternalId = "wh-riyadh-1",
            TotalAmount = 150m,
            Lines =
            [
                new ThreePLFulfillmentLine
                {
                    LineNumber = 1,
                    Sku = "SKU-3PL-1",
                    ProductName = "3PL item",
                    Quantity = 2,
                    UnitPrice = 75m,
                    LineTotal = 150m
                }
            ]
        };

    public static ConnectorContext CreateContext(Guid partnerId, Guid tenantId) =>
        new()
        {
            PartnerId = partnerId,
            TenantId = tenantId,
            ConnectorCode = ConnectorConsts.MockThreePLCode
        };
}
