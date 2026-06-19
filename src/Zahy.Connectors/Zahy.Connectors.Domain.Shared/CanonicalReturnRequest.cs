namespace Zahy.Connectors;

public sealed class CanonicalReturnLine
{
    public int LineNumber { get; init; }

    public decimal Quantity { get; init; }

    public string? Reason { get; init; }
}

public sealed class CanonicalReturnRequest
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public IReadOnlyList<CanonicalReturnLine> Lines { get; init; } = [];
}
