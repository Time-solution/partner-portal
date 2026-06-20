using System.Collections.Concurrent;

namespace Zahy.Connectors;

/// <summary>
/// In-memory custody store for the mock noon JUMP connector. Holds warehouse stock levels per
/// merchant-owned SKU and the ASN / stock-transfer history. Keyed by (partner, tenant, warehouse, sku)
/// so a connector call scoped to one partner/tenant can never read or mutate another's custody.
/// Mirrors <see cref="InMemoryThreePLConnectorTransport"/> — mock only, never wired to a real API.
/// </summary>
public sealed class InMemoryJumpConsignmentTransport
{
    private readonly ConcurrentDictionary<StockKey, decimal> _stock = new();
    private readonly List<StoredStockTransfer> _transfers = [];
    private readonly object _sync = new();

    public void Reset()
    {
        _stock.Clear();
        lock (_sync)
        {
            _transfers.Clear();
        }
    }

    /// <summary>Apply an ASN (merchant → partner warehouse), incrementing custody on-hand per SKU.</summary>
    public IReadOnlyList<CanonicalWarehouseStockLevel> ApplyStockTransfer(
        Guid partnerId,
        Guid tenantId,
        CanonicalStockTransferRequest request,
        DateTime receivedAt)
    {
        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Sku))
            {
                continue;
            }

            var key = new StockKey(partnerId, tenantId, request.WarehouseExternalId, line.Sku);
            _stock.AddOrUpdate(key, line.Quantity, (_, current) => current + line.Quantity);
        }

        lock (_sync)
        {
            _transfers.Add(new StoredStockTransfer
            {
                ExternalTransferId = request.ExternalTransferId,
                PartnerId = partnerId,
                TenantId = tenantId,
                WarehouseExternalId = request.WarehouseExternalId,
                Ownership = request.Ownership,
                ReceivedAt = receivedAt
            });
        }

        return GetStockLevels(partnerId, tenantId, request.WarehouseExternalId);
    }

    public IReadOnlyList<CanonicalWarehouseStockLevel> GetStockLevels(
        Guid partnerId,
        Guid tenantId,
        string warehouseExternalId) =>
        _stock
            .Where(kvp =>
                kvp.Key.PartnerId == partnerId &&
                kvp.Key.TenantId == tenantId &&
                string.Equals(kvp.Key.WarehouseExternalId, warehouseExternalId, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => new CanonicalWarehouseStockLevel
            {
                Sku = kvp.Key.Sku,
                OnHandQuantity = kvp.Value
            })
            .OrderBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IReadOnlyList<StoredStockTransfer> GetTransfers(Guid partnerId, Guid tenantId)
    {
        lock (_sync)
        {
            return _transfers
                .Where(t => t.PartnerId == partnerId && t.TenantId == tenantId)
                .ToList();
        }
    }

    private readonly record struct StockKey(Guid PartnerId, Guid TenantId, string WarehouseExternalId, string Sku);
}

public sealed class StoredStockTransfer
{
    public string ExternalTransferId { get; init; } = string.Empty;

    public Guid PartnerId { get; init; }

    public Guid TenantId { get; init; }

    public string WarehouseExternalId { get; init; } = string.Empty;

    public ConsignmentStockOwnership Ownership { get; init; }

    public DateTime ReceivedAt { get; init; }
}
