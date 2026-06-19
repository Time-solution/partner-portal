using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.OrderLedger;

/// <summary>
/// Append-only ledger row for one external order version. Corrections arrive as new
/// <see cref="SourceVersion"/> values — rows are never updated in place.
/// </summary>
public class OrderRecord : AggregateRoot<Guid>
{
    public Guid? TenantId { get; private set; }

    public Guid? PartnerId { get; private set; }

    public string SourceSystem { get; private set; } = string.Empty;

    public string SourceOrderId { get; private set; } = string.Empty;

    public long SourceVersion { get; private set; }

    public OrderDirection Direction { get; private set; }

    public OrderStatus Status { get; private set; }

    public PaymentStatus PaymentStatus { get; private set; }

    public string Currency { get; private set; } = OrderLedgerConsts.DefaultCurrency;

    public decimal TotalAmount { get; private set; }

    public decimal Subtotal { get; private set; }

    public OrderSubtotalResolution SubtotalResolution { get; private set; }

    public decimal TaxAmount { get; private set; }

    public decimal DeliveryFee { get; private set; }

    public DateTime CapturedAt { get; private set; }

    public DateTime SourceTimestamp { get; private set; }

    public ICollection<OrderLine> Lines { get; private set; } = new List<OrderLine>();

    protected OrderRecord()
    {
    }

    public static OrderRecord FromSnapshot(Guid id, OrderSourceSnapshot snapshot, DateTime capturedAt)
    {
        ValidateSnapshot(snapshot);

        var record = new OrderRecord
        {
            Id = id,
            TenantId = snapshot.TenantId,
            PartnerId = snapshot.PartnerId,
            SourceSystem = snapshot.SourceSystem.Trim(),
            SourceOrderId = snapshot.SourceOrderId.Trim(),
            SourceVersion = snapshot.SourceVersion,
            Direction = snapshot.Direction,
            Status = snapshot.Status,
            PaymentStatus = snapshot.PaymentStatus,
            Currency = string.IsNullOrWhiteSpace(snapshot.Currency)
                ? OrderLedgerConsts.DefaultCurrency
                : snapshot.Currency.Trim(),
            Subtotal = ResolveSubtotal(snapshot, out var subtotalResolution),
            SubtotalResolution = subtotalResolution,
            TaxAmount = snapshot.TaxAmount,
            DeliveryFee = snapshot.DeliveryFee,
            TotalAmount = snapshot.TotalAmount,
            CapturedAt = capturedAt,
            SourceTimestamp = snapshot.SourceTimestamp
        };

        foreach (var line in snapshot.Lines.OrderBy(x => x.LineNumber))
        {
            record.Lines.Add(OrderLine.FromSnapshot(id, line));
        }

        return record;
    }

    private static void ValidateSnapshot(OrderSourceSnapshot snapshot)
    {
        Check.NotNull(snapshot, nameof(snapshot));
        Check.NotNullOrWhiteSpace(snapshot.SourceSystem, nameof(snapshot.SourceSystem));
        Check.NotNullOrWhiteSpace(snapshot.SourceOrderId, nameof(snapshot.SourceOrderId));

        if (snapshot.SourceVersion <= 0)
        {
            throw new BusinessException(OrderLedgerErrorCodes.InvalidSnapshot)
                .WithData("Field", nameof(snapshot.SourceVersion));
        }
    }

    private static decimal ResolveSubtotal(
        OrderSourceSnapshot snapshot,
        out OrderSubtotalResolution resolution)
    {
        if (snapshot.Subtotal > 0)
        {
            resolution = OrderSubtotalResolution.Explicit;
            return snapshot.Subtotal;
        }

        if (snapshot.Lines.Count > 0)
        {
            resolution = OrderSubtotalResolution.FromLines;
            return snapshot.Lines.Sum(x => x.LineTotal);
        }

        // Never infer commission basis from TotalAmount — it includes tax and delivery.
        resolution = OrderSubtotalResolution.Unavailable;
        return 0m;
    }

    public bool HasReliableCommissionSubtotal() =>
        SubtotalResolution != OrderSubtotalResolution.Unavailable;
}
