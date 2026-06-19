namespace Zahy.Connectors;

public sealed class CanonicalShipmentRate
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Currency { get; init; } = ConnectorConsts.DefaultCurrency;

    public string ServiceLevel { get; init; } = string.Empty;

    public DateTime QuotedAt { get; init; }
}

public sealed class CanonicalShipmentLabel
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public string TrackingNumber { get; init; } = string.Empty;

    public string LabelReference { get; init; } = string.Empty;

    public decimal RateAmount { get; init; }

    public string Currency { get; init; } = ConnectorConsts.DefaultCurrency;

    public DateTime CreatedAt { get; init; }
}

public sealed class CanonicalInventoryReconcileLine
{
    public string Sku { get; init; } = string.Empty;

    public decimal ExpectedQuantity { get; init; }

    public decimal ReportedQuantity { get; init; }

    public decimal Delta { get; init; }
}

public sealed class CanonicalInventoryReconcileResult
{
    public string WarehouseExternalId { get; init; } = string.Empty;

    public IReadOnlyList<CanonicalInventoryReconcileLine> Lines { get; init; } = [];

    public DateTime ReconciledAt { get; init; }
}
