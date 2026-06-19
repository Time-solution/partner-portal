namespace Zahy.Connectors;

internal static class CarrierTestData
{
    public static CarrierShipmentRequest CreateShipmentRequest(
        Guid partnerId,
        Guid tenantId,
        string externalOrderId) =>
        new()
        {
            ExternalOrderId = externalOrderId,
            PartnerId = partnerId,
            TenantId = tenantId,
            WeightKg = 2.5m,
            DestinationCity = "Jeddah",
            PickupOutletExternalId = "pickup-001",
            DeclaredValue = 120m
        };

    public static ConnectorContext CreateContext(Guid partnerId, Guid tenantId) =>
        new()
        {
            PartnerId = partnerId,
            TenantId = tenantId,
            ConnectorCode = ConnectorConsts.MockCarrierCode
        };
}
