namespace Zahy.Connectors;

public sealed class CanonicalMenuItem
{
    public string ExternalItemId { get; init; } = string.Empty;

    public string? Sku { get; init; }

    public string NameEn { get; init; } = string.Empty;

    public string? NameAr { get; init; }

    public string? DescriptionEn { get; init; }

    public string? DescriptionAr { get; init; }

    public decimal Price { get; init; }

    public string Currency { get; init; } = ConnectorConsts.DefaultCurrency;

    public bool IsAvailable { get; init; }

    public string? CategoryExternalId { get; init; }

    public string? OutletExternalId { get; init; }
}
