using System;
using System.Collections.Generic;

namespace Zahy.OrderLedger;

public sealed class OrderLineSnapshot
{
    public int LineNumber { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }
}

public sealed class OrderSourceSnapshot
{
    public string SourceSystem { get; init; } = OrderLedgerConsts.FakeSourceSystem;

    public string SourceOrderId { get; init; } = string.Empty;

    public long SourceVersion { get; init; }

    public Guid? TenantId { get; init; }

    public Guid? PartnerId { get; init; }

    public OrderDirection Direction { get; init; }

    public OrderStatus Status { get; init; }

    public PaymentStatus PaymentStatus { get; init; }

    /// <summary>Merchandise subtotal — commission basis default (excludes tax and delivery).</summary>
    public decimal Subtotal { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal DeliveryFee { get; init; }

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = OrderLedgerConsts.DefaultCurrency;

    public IReadOnlyList<OrderLineSnapshot> Lines { get; init; } = [];

    public DateTime SourceTimestamp { get; init; }
}

public sealed class OrderSourcePollRequest
{
    public string SourceSystem { get; init; } = OrderLedgerConsts.FakeSourceSystem;

    public DateTime? Since { get; init; }

    public int MaxResults { get; init; } = 100;
}
