using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Zahy.OrderLedger;

public class FakeOrderSourcePollTests : ZahyOrderLedgerTestBase
{
    private readonly FakeOrderSource _fakeOrderSource;
    private readonly IOrderLedgerIngestionService _ingestionService;
    private readonly IRepository<OrderRecord, Guid> _orderRecordRepository;

    public FakeOrderSourcePollTests()
    {
        _fakeOrderSource = GetRequiredService<FakeOrderSource>();
        _ingestionService = GetRequiredService<IOrderLedgerIngestionService>();
        _orderRecordRepository = GetRequiredService<IRepository<OrderRecord, Guid>>();
    }

    [Fact]
    public async Task Should_Ingest_Snapshots_From_Fake_IOrderSource()
    {
        _fakeOrderSource.Reset();
        _fakeOrderSource.Seed(CreateSnapshot("poll-1", 1));
        _fakeOrderSource.Seed(CreateSnapshot("poll-2", 1));

        OrderIngestBatchResult firstBatch = null!;
        OrderIngestBatchResult secondBatch = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            firstBatch = await _ingestionService.PollAndIngestAsync(new OrderSourcePollRequest());
        });

        firstBatch.PolledCount.ShouldBe(2);
        firstBatch.IngestedCount.ShouldBe(2);
        firstBatch.SkippedDuplicateCount.ShouldBe(0);

        await WithUnitOfWorkAsync(async () =>
        {
            secondBatch = await _ingestionService.PollAndIngestAsync(new OrderSourcePollRequest());
        });

        secondBatch.IngestedCount.ShouldBe(0);
        secondBatch.SkippedDuplicateCount.ShouldBe(2);

        await WithUnitOfWorkAsync(async () =>
        {
            (await _orderRecordRepository.GetCountAsync()).ShouldBe(2);
        });
    }

    private static OrderSourceSnapshot CreateSnapshot(string sourceOrderId, long version) =>
        new()
        {
            SourceSystem = OrderLedgerConsts.FakeSourceSystem,
            SourceOrderId = sourceOrderId,
            SourceVersion = version,
            TenantId = Guid.NewGuid(),
            Direction = OrderDirection.Outbound,
            Status = OrderStatus.Created,
            PaymentStatus = PaymentStatus.Unpaid,
            TotalAmount = 50m,
            SourceTimestamp = DateTime.UtcNow,
            Lines =
            [
                new OrderLineSnapshot
                {
                    LineNumber = 1,
                    Sku = "SKU-P",
                    ProductName = "Polled item",
                    Quantity = 1,
                    UnitPrice = 50m,
                    LineTotal = 50m
                }
            ]
        };
}
