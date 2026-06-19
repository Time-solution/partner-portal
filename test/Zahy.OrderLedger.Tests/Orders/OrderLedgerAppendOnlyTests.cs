using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Zahy.OrderLedger;

public class OrderLedgerAppendOnlyTests : ZahyOrderLedgerTestBase
{
    private readonly IOrderLedgerIngestionService _ingestionService;
    private readonly IRepository<OrderRecord, Guid> _orderRecordRepository;

    public OrderLedgerAppendOnlyTests()
    {
        _ingestionService = GetRequiredService<IOrderLedgerIngestionService>();
        _orderRecordRepository = GetRequiredService<IRepository<OrderRecord, Guid>>();
    }

    [Fact]
    public async Task Should_Append_New_Version_Instead_Of_Updating_Existing_Row()
    {
        Guid versionOneId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var v1 = await _ingestionService.IngestSnapshotAsync(CreateSnapshot(version: 1, total: 100m, status: OrderStatus.Created));
            versionOneId = v1.OrderRecordId;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var v2 = await _ingestionService.IngestSnapshotAsync(CreateSnapshot(version: 2, total: 120m, status: OrderStatus.Paid));
            v2.IsNew.ShouldBeTrue();
            v2.OrderRecordId.ShouldNotBe(versionOneId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var records = (await _orderRecordRepository.GetListAsync(x => x.SourceOrderId == "append-only-1"))
                .OrderBy(x => x.SourceVersion)
                .ToList();

            records.Count.ShouldBe(2);
            records[0].SourceVersion.ShouldBe(1);
            records[0].TotalAmount.ShouldBe(100m);
            records[0].Status.ShouldBe(OrderStatus.Created);
            records[1].SourceVersion.ShouldBe(2);
            records[1].TotalAmount.ShouldBe(120m);
            records[1].Status.ShouldBe(OrderStatus.Paid);
        });
    }

    private static OrderSourceSnapshot CreateSnapshot(long version, decimal total, OrderStatus status) =>
        new()
        {
            SourceSystem = OrderLedgerConsts.FakeSourceSystem,
            SourceOrderId = "append-only-1",
            SourceVersion = version,
            TenantId = Guid.NewGuid(),
            Direction = OrderDirection.Inbound,
            Status = status,
            PaymentStatus = status == OrderStatus.Paid ? PaymentStatus.Paid : PaymentStatus.Unpaid,
            TotalAmount = total,
            SourceTimestamp = DateTime.UtcNow.AddMinutes(version),
            Lines =
            [
                new OrderLineSnapshot
                {
                    LineNumber = 1,
                    Sku = "SKU-A",
                    ProductName = "Item A",
                    Quantity = 1,
                    UnitPrice = total,
                    LineTotal = total
                }
            ]
        };
}
