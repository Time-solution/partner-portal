using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.OrderLedger;

namespace Zahy.Connectors;

public class ConnectorOrderIngestionTests : ZahyConnectorsOrderLedgerTestBase
{
    private readonly IConnectorOrderIngestionService _connectorIngestionService;
    private readonly IRepository<OrderRecord, Guid> _orderRecordRepository;
    private readonly InMemoryAggregatorConnectorTransport _transport;
    private readonly Volo.Abp.Timing.IClock _clock;

    public ConnectorOrderIngestionTests()
    {
        _connectorIngestionService = GetRequiredService<IConnectorOrderIngestionService>();
        _orderRecordRepository = GetRequiredService<IRepository<OrderRecord, Guid>>();
        _transport = GetRequiredService<InMemoryAggregatorConnectorTransport>();
        _clock = GetRequiredService<Volo.Abp.Timing.IClock>();
    }

    [Fact]
    public async Task Should_Ingest_Aggregator_Order_Into_Ledger()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.EnqueueOrder(AggregatorTestData.CreateInboundOrder(
            partnerId, tenantId, "ledger-order-1", _clock.Now));

        await WithUnitOfWorkAsync(async () =>
        {
            var result = await _connectorIngestionService.IngestReceivedOrderAsync(
                AggregatorTestData.CreateContext(partnerId, tenantId),
                AggregatorTestData.CreateReceiveRequest("ledger-order-1"));

            result.IsNew.ShouldBeTrue();
            result.SourceOrderId.ShouldBe("ledger-order-1");

            var records = await _orderRecordRepository.GetListAsync();
            records.Count.ShouldBe(1);
            records[0].SourceSystem.ShouldBe("connector:aggregator:mock-aggregator");
            records[0].PartnerId.ShouldBe(partnerId);
            records[0].TenantId.ShouldBe(tenantId);
        });
    }

    [Fact]
    public async Task Duplicate_Aggregator_Receive_Does_Not_Create_Duplicate_Ledger_Entry()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        _transport.EnqueueOrder(AggregatorTestData.CreateInboundOrder(
            partnerId, tenantId, "dup-ledger-order", _clock.Now));

        await WithUnitOfWorkAsync(async () =>
        {
            var first = await _connectorIngestionService.IngestReceivedOrderAsync(
                AggregatorTestData.CreateContext(partnerId, tenantId),
                AggregatorTestData.CreateReceiveRequest("dup-ledger-order"));
            first.IsNew.ShouldBeTrue();

            var second = await _connectorIngestionService.IngestReceivedOrderAsync(
                AggregatorTestData.CreateContext(partnerId, tenantId),
                AggregatorTestData.CreateReceiveRequest("dup-ledger-order"));
            second.IsNew.ShouldBeFalse();

            var records = await _orderRecordRepository.GetListAsync();
            records.Count.ShouldBe(1);
            records.Count(x =>
                x.SourceSystem == "connector:aggregator:mock-aggregator" &&
                x.SourceOrderId == "dup-ledger-order" &&
                x.SourceVersion == 1).ShouldBe(1);
        });
    }
}
