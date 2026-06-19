using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.OrderLedger;

public class OrderLine : Entity<Guid>
{
    public Guid OrderRecordId { get; internal set; }

    public int LineNumber { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal { get; private set; }

    protected OrderLine()
    {
    }

    public static OrderLine FromSnapshot(Guid orderRecordId, OrderLineSnapshot snapshot)
    {
        Check.NotNull(snapshot, nameof(snapshot));

        return new OrderLine
        {
            Id = Guid.NewGuid(),
            OrderRecordId = orderRecordId,
            LineNumber = snapshot.LineNumber,
            Sku = snapshot.Sku.Trim(),
            ProductName = snapshot.ProductName.Trim(),
            Quantity = snapshot.Quantity,
            UnitPrice = snapshot.UnitPrice,
            LineTotal = snapshot.LineTotal
        };
    }
}
