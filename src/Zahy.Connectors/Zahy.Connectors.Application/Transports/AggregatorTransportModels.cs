namespace Zahy.Connectors;

/// <summary>Partner-shaped inbound order payload (aggregator transport). Mapped to <see cref="CanonicalOrder"/> by the adapter.</summary>
public sealed class AggregatorInboundOrder
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public Guid PartnerId { get; init; }

    public Guid TenantId { get; init; }

    public string OutletExternalId { get; init; } = string.Empty;

    public DateTime PlacedAt { get; init; }

    public int AcceptWithinMinutes { get; init; } = ConnectorConsts.DefaultAcceptWindowMinutes;

    public DateTime? AcceptDeadlineUtc { get; set; }

    public AggregatorInboundOrderStatus Status { get; set; } = AggregatorInboundOrderStatus.New;

    public decimal Subtotal { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal DeliveryFee { get; init; }

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = ConnectorConsts.DefaultCurrency;

    public CanonicalPaymentState PaymentState { get; init; } = CanonicalPaymentState.Unpaid;

    public string? CustomerNotes { get; init; }

    public IReadOnlyList<AggregatorInboundOrderLine> Lines { get; init; } = [];
}

public sealed class AggregatorInboundOrderLine
{
    public int LineNumber { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }
}

public enum AggregatorInboundOrderStatus
{
    New = 1,
    Accepted = 2,
    Rejected = 3
}

public sealed class AggregatorTransportMenuItem
{
    public string ExternalItemId { get; init; } = string.Empty;

    public string? Sku { get; init; }

    public string NameEn { get; init; } = string.Empty;

    public string? NameAr { get; init; }

    public decimal Price { get; init; }

    public string Currency { get; init; } = ConnectorConsts.DefaultCurrency;

    public bool IsAvailable { get; init; } = true;

    public string? OutletExternalId { get; init; }
}
