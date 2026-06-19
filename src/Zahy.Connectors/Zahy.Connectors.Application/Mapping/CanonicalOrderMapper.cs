using Zahy.OrderLedger;

namespace Zahy.Connectors;

public class CanonicalOrderMapper : ICanonicalOrderMapper
{
    public OrderSourceSnapshot ToSnapshot(CanonicalOrder order)
    {
        return new OrderSourceSnapshot
        {
            SourceSystem = ConnectorSourceSystem.Build(order.ConnectorKind, order.ConnectorCode),
            SourceOrderId = order.ExternalOrderId,
            SourceVersion = order.Version,
            TenantId = order.TenantId,
            PartnerId = order.PartnerId,
            Direction = MapDirection(order.Direction),
            Status = MapStatusToLedger(order.Status),
            PaymentStatus = MapPayment(order.PaymentState),
            Subtotal = order.Subtotal,
            TaxAmount = order.TaxAmount,
            DeliveryFee = order.DeliveryFee,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            SourceTimestamp = order.PlacedAt,
            Lines = order.Lines.Select(line => new OrderLineSnapshot
            {
                LineNumber = line.LineNumber,
                Sku = line.Sku,
                ProductName = line.ProductName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal
            }).ToList()
        };
    }

    public OrderSourceSnapshot ToSnapshot(OrderRecord record) =>
        new()
        {
            SourceSystem = record.SourceSystem,
            SourceOrderId = record.SourceOrderId,
            SourceVersion = record.SourceVersion,
            TenantId = record.TenantId,
            PartnerId = record.PartnerId,
            Direction = record.Direction,
            Status = record.Status,
            PaymentStatus = record.PaymentStatus,
            Subtotal = record.Subtotal,
            TaxAmount = record.TaxAmount,
            DeliveryFee = record.DeliveryFee,
            TotalAmount = record.TotalAmount,
            Currency = record.Currency,
            SourceTimestamp = record.SourceTimestamp,
            Lines = record.Lines
                .OrderBy(x => x.LineNumber)
                .Select(line => new OrderLineSnapshot
                {
                    LineNumber = line.LineNumber,
                    Sku = line.Sku,
                    ProductName = line.ProductName,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    LineTotal = line.LineTotal
                })
                .ToList()
        };

    public OrderSourceSnapshot ToStatusSnapshot(
        OrderSourceSnapshot latestSnapshot,
        CanonicalOrderStatusUpdate update)
    {
        var nextVersion = update.Version > latestSnapshot.SourceVersion
            ? update.Version
            : latestSnapshot.SourceVersion + 1;

        return new OrderSourceSnapshot
        {
            SourceSystem = latestSnapshot.SourceSystem,
            SourceOrderId = latestSnapshot.SourceOrderId,
            SourceVersion = nextVersion,
            TenantId = latestSnapshot.TenantId,
            PartnerId = latestSnapshot.PartnerId,
            Direction = latestSnapshot.Direction,
            Status = MapStatusToLedger(update.NewStatus),
            PaymentStatus = update.PaymentState.HasValue
                ? MapPayment(update.PaymentState.Value)
                : latestSnapshot.PaymentStatus,
            Subtotal = latestSnapshot.Subtotal,
            TaxAmount = latestSnapshot.TaxAmount,
            DeliveryFee = latestSnapshot.DeliveryFee,
            TotalAmount = latestSnapshot.TotalAmount,
            Currency = latestSnapshot.Currency,
            SourceTimestamp = update.OccurredAt,
            Lines = latestSnapshot.Lines
        };
    }

    public CanonicalOrder FromSnapshot(
        OrderSourceSnapshot snapshot,
        ConnectorKind connectorKind,
        string connectorCode)
    {
        if (!ConnectorSourceSystem.TryParse(snapshot.SourceSystem, out var parsedKind, out var parsedCode))
        {
            parsedKind = connectorKind;
            parsedCode = connectorCode;
        }

        return new CanonicalOrder
        {
            ExternalOrderId = snapshot.SourceOrderId,
            Version = snapshot.SourceVersion,
            ConnectorCode = parsedCode,
            ConnectorKind = parsedKind,
            PartnerId = snapshot.PartnerId ?? Guid.Empty,
            TenantId = snapshot.TenantId ?? Guid.Empty,
            Direction = MapDirectionFromLedger(snapshot.Direction),
            Status = MapStatusFromLedger(snapshot.Status),
            PaymentState = MapPaymentFromLedger(snapshot.PaymentStatus),
            TotalAmount = snapshot.TotalAmount,
            Currency = snapshot.Currency,
            PlacedAt = snapshot.SourceTimestamp,
            Lines = snapshot.Lines.Select(line => new CanonicalOrderLine
            {
                LineNumber = line.LineNumber,
                Sku = line.Sku,
                ProductName = line.ProductName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal
            }).ToList()
        };
    }

    private static OrderDirection MapDirection(CanonicalOrderDirection direction) =>
        direction switch
        {
            CanonicalOrderDirection.Inbound => OrderDirection.Inbound,
            CanonicalOrderDirection.Internal => OrderDirection.Internal,
            _ => OrderDirection.Outbound
        };

    private static CanonicalOrderDirection MapDirectionFromLedger(OrderDirection direction) =>
        direction switch
        {
            OrderDirection.Inbound => CanonicalOrderDirection.Inbound,
            OrderDirection.Internal => CanonicalOrderDirection.Internal,
            _ => CanonicalOrderDirection.Outbound
        };

    private static OrderStatus MapStatusToLedger(CanonicalOrderStatus status) =>
        status switch
        {
            CanonicalOrderStatus.Delivered => OrderStatus.Fulfilled,
            CanonicalOrderStatus.Rejected or
            CanonicalOrderStatus.Cancelled or
            CanonicalOrderStatus.Returned or
            CanonicalOrderStatus.Failed => OrderStatus.Cancelled,
            CanonicalOrderStatus.InTransit => OrderStatus.Paid,
            _ => OrderStatus.Created
        };

    private static CanonicalOrderStatus MapStatusFromLedger(OrderStatus status) =>
        status switch
        {
            OrderStatus.Fulfilled => CanonicalOrderStatus.Delivered,
            OrderStatus.Cancelled => CanonicalOrderStatus.Cancelled,
            OrderStatus.Paid => CanonicalOrderStatus.InTransit,
            _ => CanonicalOrderStatus.Received
        };

    private static PaymentStatus MapPayment(CanonicalPaymentState paymentState) =>
        paymentState switch
        {
            CanonicalPaymentState.Paid => PaymentStatus.Paid,
            CanonicalPaymentState.Refunded => PaymentStatus.Refunded,
            _ => PaymentStatus.Unpaid
        };

    private static CanonicalPaymentState MapPaymentFromLedger(PaymentStatus paymentStatus) =>
        paymentStatus switch
        {
            PaymentStatus.Paid => CanonicalPaymentState.Paid,
            PaymentStatus.Refunded => CanonicalPaymentState.Refunded,
            _ => CanonicalPaymentState.Unpaid
        };
}
