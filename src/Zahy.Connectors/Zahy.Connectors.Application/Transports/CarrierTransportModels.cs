namespace Zahy.Connectors;

public sealed class CarrierShipmentRequest
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public Guid PartnerId { get; init; }

    public Guid TenantId { get; init; }

    public decimal WeightKg { get; init; }

    public string DestinationCity { get; init; } = string.Empty;

    public string PickupOutletExternalId { get; init; } = string.Empty;

    public decimal DeclaredValue { get; init; }

    public string Currency { get; init; } = ConnectorConsts.DefaultCurrency;
}

public sealed class CarrierShipmentRecord
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public Guid PartnerId { get; init; }

    public Guid TenantId { get; init; }

    public string TrackingNumber { get; set; } = string.Empty;

    public string LabelReference { get; set; } = string.Empty;

    public decimal RateAmount { get; set; }

    public string Currency { get; init; } = ConnectorConsts.DefaultCurrency;

    public CanonicalOrderStatus Status { get; set; } = CanonicalOrderStatus.ReadyForHandoff;
}
