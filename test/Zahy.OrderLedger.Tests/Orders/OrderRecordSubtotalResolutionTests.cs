using System;
using Shouldly;
using Xunit;

namespace Zahy.OrderLedger;

public class OrderRecordSubtotalResolutionTests
{
    [Fact]
    public void Should_Mark_Subtotal_Unavailable_When_No_Explicit_Subtotal_And_No_Lines()
    {
        var record = OrderRecord.FromSnapshot(
            Guid.NewGuid(),
            new OrderSourceSnapshot
            {
                SourceSystem = OrderLedgerConsts.FakeSourceSystem,
                SourceOrderId = "total-only",
                SourceVersion = 1,
                PaymentStatus = PaymentStatus.Paid,
                Status = OrderStatus.Paid,
                Subtotal = 0m,
                TaxAmount = 15m,
                DeliveryFee = 20m,
                TotalAmount = 135m,
                SourceTimestamp = DateTime.UtcNow
            },
            DateTime.UtcNow);

        record.Subtotal.ShouldBe(0m);
        record.SubtotalResolution.ShouldBe(OrderSubtotalResolution.Unavailable);
        record.HasReliableCommissionSubtotal().ShouldBeFalse();
    }

    [Fact]
    public void Should_Not_Fallback_Subtotal_To_TotalAmount()
    {
        var record = OrderRecord.FromSnapshot(
            Guid.NewGuid(),
            new OrderSourceSnapshot
            {
                SourceSystem = OrderLedgerConsts.FakeSourceSystem,
                SourceOrderId = "no-lines",
                SourceVersion = 1,
                Subtotal = 0m,
                TaxAmount = 10m,
                DeliveryFee = 5m,
                TotalAmount = 115m,
                SourceTimestamp = DateTime.UtcNow
            },
            DateTime.UtcNow);

        record.Subtotal.ShouldBe(0m);
        record.Subtotal.ShouldNotBe(record.TotalAmount);
    }

    [Fact]
    public void Should_Resolve_Subtotal_From_Lines()
    {
        var record = OrderRecord.FromSnapshot(
            Guid.NewGuid(),
            new OrderSourceSnapshot
            {
                SourceSystem = OrderLedgerConsts.FakeSourceSystem,
                SourceOrderId = "line-sum",
                SourceVersion = 1,
                Subtotal = 0m,
                TotalAmount = 135m,
                SourceTimestamp = DateTime.UtcNow,
                Lines =
                [
                    new OrderLineSnapshot
                    {
                        LineNumber = 1,
                        Sku = "A",
                        ProductName = "A",
                        Quantity = 1,
                        UnitPrice = 80m,
                        LineTotal = 80m
                    },
                    new OrderLineSnapshot
                    {
                        LineNumber = 2,
                        Sku = "B",
                        ProductName = "B",
                        Quantity = 1,
                        UnitPrice = 20m,
                        LineTotal = 20m
                    }
                ]
            },
            DateTime.UtcNow);

        record.Subtotal.ShouldBe(100m);
        record.SubtotalResolution.ShouldBe(OrderSubtotalResolution.FromLines);
        record.HasReliableCommissionSubtotal().ShouldBeTrue();
    }
}
