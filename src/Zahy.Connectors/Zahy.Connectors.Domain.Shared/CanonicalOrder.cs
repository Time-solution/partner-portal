namespace Zahy.Connectors;

public sealed class CanonicalOrder
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public long Version { get; init; }

    public string ConnectorCode { get; init; } = string.Empty;

    public ConnectorKind ConnectorKind { get; init; }

    public Guid PartnerId { get; init; }

    public Guid TenantId { get; init; }

    public string? OutletExternalId { get; init; }

    public Guid? OutletId { get; init; }

    public CanonicalOrderDirection Direction { get; init; }

    public CanonicalOrderStatus Status { get; init; }

    public CanonicalPaymentState PaymentState { get; init; }

    public string Currency { get; init; } = ConnectorConsts.DefaultCurrency;

    public decimal Subtotal { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal DeliveryFee { get; init; }

    public decimal TotalAmount { get; init; }

    public IReadOnlyList<CanonicalOrderLine> Lines { get; init; } = [];

    public DateTime PlacedAt { get; init; }

    public DateTime? AcceptDeadlineUtc { get; init; }

    public string? CustomerNotes { get; init; }

    public string? DeliveryAddressSummary { get; init; }
}
