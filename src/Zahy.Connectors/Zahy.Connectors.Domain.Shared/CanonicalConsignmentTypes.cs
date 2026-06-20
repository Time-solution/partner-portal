namespace Zahy.Connectors;

/// <summary>
/// Who owns stock held in a partner's warehouse under a consignment ("Fulfilled by") arrangement.
/// Connector-local mirror of the Partner Catalog ConsignmentOwnershipMode so the connector stays
/// self-contained; the catalog layer maps it to the existing settlement participation mode.
/// </summary>
public enum ConsignmentStockOwnership
{
    /// <summary>Merchant still owns the goods the partner holds (true consignment).</summary>
    MerchantOwned = 1,

    /// <summary>Partner bought the goods up-front and owns them.</summary>
    PartnerBought = 2
}

/// <summary>
/// Canonical ASN / stock-transfer (merchant → partner warehouse) for consignment custody.
/// Mirrors the existing 3PL canonical shapes for a clean live swap of the noon JUMP connector.
/// </summary>
public sealed class CanonicalStockTransferRequest
{
    public string ExternalTransferId { get; init; } = string.Empty;

    public string WarehouseExternalId { get; init; } = string.Empty;

    /// <summary>Owner of the stock at the partner warehouse (custody semantics).</summary>
    public ConsignmentStockOwnership Ownership { get; init; } = ConsignmentStockOwnership.MerchantOwned;

    public IReadOnlyList<CanonicalStockTransferLine> Lines { get; init; } = [];
}

public sealed class CanonicalStockTransferLine
{
    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal Quantity { get; init; }
}

public sealed class CanonicalStockTransferResult
{
    public string ExternalTransferId { get; init; } = string.Empty;

    public string WarehouseExternalId { get; init; } = string.Empty;

    public ConsignmentStockOwnership Ownership { get; init; }

    /// <summary>Warehouse on-hand per merchant-owned SKU after applying this transfer.</summary>
    public IReadOnlyList<CanonicalWarehouseStockLevel> StockLevels { get; init; } = [];

    public DateTime ReceivedAt { get; init; }
}

/// <summary>On-hand quantity of one SKU held in custody at the partner warehouse.</summary>
public sealed class CanonicalWarehouseStockLevel
{
    public string Sku { get; init; } = string.Empty;

    public decimal OnHandQuantity { get; init; }
}
