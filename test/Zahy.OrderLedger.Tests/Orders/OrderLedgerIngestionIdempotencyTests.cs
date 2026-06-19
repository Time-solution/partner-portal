using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Zahy.OrderLedger;

public class OrderLedgerIngestionIdempotencyTests : ZahyOrderLedgerTestBase
{
    private readonly IOrderLedgerIngestionService _ingestionService;
    private readonly IRepository<OrderRecord, Guid> _orderRecordRepository;

    public OrderLedgerIngestionIdempotencyTests()
    {
        _ingestionService = GetRequiredService<IOrderLedgerIngestionService>();
        _orderRecordRepository = GetRequiredService<IRepository<OrderRecord, Guid>>();
    }

    [Fact]
    public async Task Should_Create_Single_Ledger_Entry_For_Duplicate_Source_Id_And_Version()
    {
        var snapshot = CreateSnapshot("order-1", version: 1, total: 150m);

        OrderIngestResult first = null!;
        OrderIngestResult second = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            first = await _ingestionService.IngestSnapshotAsync(snapshot);
            second = await _ingestionService.IngestSnapshotAsync(snapshot);
        });

        first.IsNew.ShouldBeTrue();
        second.IsNew.ShouldBeFalse();
        second.OrderRecordId.ShouldBe(first.OrderRecordId);

        await WithUnitOfWorkAsync(async () =>
        {
            var records = await _orderRecordRepository.GetListAsync(x => x.SourceOrderId == "order-1");
            records.Count.ShouldBe(1);
            records.Single().TotalAmount.ShouldBe(150m);
        });
    }

    private static OrderSourceSnapshot CreateSnapshot(string sourceOrderId, long version, decimal total) =>
        new()
        {
            SourceSystem = OrderLedgerConsts.FakeSourceSystem,
            SourceOrderId = sourceOrderId,
            SourceVersion = version,
            TenantId = Guid.NewGuid(),
            Direction = OrderDirection.Outbound,
            Status = OrderStatus.Created,
            PaymentStatus = PaymentStatus.Unpaid,
            TotalAmount = total,
            SourceTimestamp = DateTime.UtcNow,
            Lines =
            [
                new OrderLineSnapshot
                {
                    LineNumber = 1,
                    Sku = "SKU-1",
                    ProductName = "Test product",
                    Quantity = 1,
                    UnitPrice = total,
                    LineTotal = total
                }
            ]
        };
}
