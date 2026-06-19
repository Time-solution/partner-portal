using System.Collections.Concurrent;

namespace Zahy.Connectors;

public sealed class InMemoryThreePLConnectorTransport
{
    private readonly ConcurrentDictionary<string, ThreePLFulfillmentOrder> _orders = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ThreePLInventoryRecord> _inventory = [];
    private readonly List<ThreePLReturnRecord> _returns = [];

    public void Reset()
    {
        _orders.Clear();
        _inventory.Clear();
        _returns.Clear();
    }

    public void StageOrder(ThreePLFulfillmentOrder order) =>
        _orders[order.ExternalOrderId] = Clone(order);

    public bool TryGetOrder(string externalOrderId, out ThreePLFulfillmentOrder order) =>
        _orders.TryGetValue(externalOrderId, out order!);

    public void MarkPushed(string externalOrderId)
    {
        if (_orders.TryGetValue(externalOrderId, out var order))
        {
            order.Status = ThreePLFulfillmentStatus.Pushed;
        }
    }

    public void UpdateStatus(string externalOrderId, ThreePLFulfillmentStatus status, string? trackingNumber = null)
    {
        if (_orders.TryGetValue(externalOrderId, out var order))
        {
            order.Status = status;
            if (!string.IsNullOrWhiteSpace(trackingNumber))
            {
                order.TrackingNumber = trackingNumber;
            }
        }
    }

    public void SetExpectedInventory(IEnumerable<ThreePLInventoryRecord> records)
    {
        _inventory.Clear();
        _inventory.AddRange(records);
    }

    public IReadOnlyList<ThreePLInventoryRecord> GetExpectedInventory() => _inventory;

    public void RecordReturn(ThreePLReturnRecord record) => _returns.Add(record);

    public IReadOnlyList<ThreePLReturnRecord> GetReturns() => _returns;

    private static ThreePLFulfillmentOrder Clone(ThreePLFulfillmentOrder order) =>
        new()
        {
            ExternalOrderId = order.ExternalOrderId,
            PartnerId = order.PartnerId,
            TenantId = order.TenantId,
            WarehouseExternalId = order.WarehouseExternalId,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            TrackingNumber = order.TrackingNumber,
            Lines = order.Lines
        };
}
