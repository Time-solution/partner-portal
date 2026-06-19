namespace Zahy.Connectors;

public sealed class CanonicalOrderLine
{
    public int LineNumber { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }

    public string? Notes { get; init; }
}
