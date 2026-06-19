namespace Zahy.Connectors;

public sealed class ThreePLFulfillmentOrder
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public Guid PartnerId { get; init; }

    public Guid TenantId { get; init; }

    public string WarehouseExternalId { get; init; } = string.Empty;

    public ThreePLFulfillmentStatus Status { get; set; } = ThreePLFulfillmentStatus.Staged;

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = ConnectorConsts.DefaultCurrency;

    public string? TrackingNumber { get; set; }

    public IReadOnlyList<ThreePLFulfillmentLine> Lines { get; init; } = [];
}

public sealed class ThreePLFulfillmentLine
{
    public int LineNumber { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }
}

public enum ThreePLFulfillmentStatus
{
    Staged = 1,
    Pushed = 2,
    Picking = 3,
    Shipped = 4,
    Returned = 5
}

public sealed class ThreePLInventoryRecord
{
    public string Sku { get; init; } = string.Empty;

    public decimal ExpectedQuantity { get; init; }
}

public sealed class ThreePLReturnRecord
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public DateTime RecordedAt { get; init; }
}
