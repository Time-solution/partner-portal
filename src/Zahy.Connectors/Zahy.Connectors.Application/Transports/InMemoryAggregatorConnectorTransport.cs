using System.Collections.Concurrent;

namespace Zahy.Connectors;

public sealed class InMemoryAggregatorConnectorTransport
{
    private readonly ConcurrentDictionary<string, AggregatorInboundOrder> _orders = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AggregatorTransportMenuItem> _menuItems = [];

    public void Reset()
    {
        _orders.Clear();
        _menuItems.Clear();
    }

    public void EnqueueOrder(AggregatorInboundOrder order)
    {
        var normalized = new AggregatorInboundOrder
        {
            ExternalOrderId = order.ExternalOrderId,
            PartnerId = order.PartnerId,
            TenantId = order.TenantId,
            OutletExternalId = order.OutletExternalId,
            PlacedAt = order.PlacedAt,
            AcceptWithinMinutes = order.AcceptWithinMinutes,
            AcceptDeadlineUtc = order.AcceptDeadlineUtc ?? order.PlacedAt.AddMinutes(order.AcceptWithinMinutes),
            Status = order.Status,
            Subtotal = order.Subtotal,
            TaxAmount = order.TaxAmount,
            DeliveryFee = order.DeliveryFee,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            PaymentState = order.PaymentState,
            CustomerNotes = order.CustomerNotes,
            Lines = order.Lines
        };

        _orders[normalized.ExternalOrderId] = normalized;
    }

    public bool TryGetOrder(string externalOrderId, out AggregatorInboundOrder order) =>
        _orders.TryGetValue(externalOrderId, out order!);

    public void UpdateOrderStatus(string externalOrderId, AggregatorInboundOrderStatus status)
    {
        if (_orders.TryGetValue(externalOrderId, out var existing))
        {
            existing.Status = status;
        }
    }

    public void SetMenuItems(IEnumerable<AggregatorTransportMenuItem> items)
    {
        _menuItems.Clear();
        _menuItems.AddRange(items);
    }

    public IReadOnlyList<AggregatorTransportMenuItem> GetMenuItems(string? outletExternalId) =>
        _menuItems
            .Where(x => outletExternalId == null ||
                        string.Equals(x.OutletExternalId, outletExternalId, StringComparison.OrdinalIgnoreCase))
            .ToList();
}
