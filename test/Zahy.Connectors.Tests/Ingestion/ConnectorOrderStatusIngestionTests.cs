using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;
using Zahy.OrderLedger;

namespace Zahy.Connectors;

public class ConnectorOrderStatusIngestionTests : ZahyConnectorsOrderLedgerTestBase
{
    private readonly IConnectorOrderIngestionService _connectorIngestionService;
    private readonly IRepository<OrderRecord, Guid> _orderRecordRepository;
    private readonly InMemoryThreePLConnectorTransport _threePLTransport;
    private readonly InMemoryCarrierConnectorTransport _carrierTransport;
    private readonly IClock _clock;

    public ConnectorOrderStatusIngestionTests()
    {
        _connectorIngestionService = GetRequiredService<IConnectorOrderIngestionService>();
        _orderRecordRepository = GetRequiredService<IRepository<OrderRecord, Guid>>();
        _threePLTransport = GetRequiredService<InMemoryThreePLConnectorTransport>();
        _carrierTransport = GetRequiredService<InMemoryCarrierConnectorTransport>();
        _clock = GetRequiredService<IClock>();
    }

    [Fact]
    public async Task Should_Append_ThreePL_Status_Update_As_New_SourceVersion_Not_Duplicate_Order()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        const string externalOrderId = "3pl-versioned-order";
        _threePLTransport.Reset();
        _threePLTransport.StageOrder(ThreePLTestData.CreateFulfillmentOrder(partnerId, tenantId, externalOrderId));

        await WithUnitOfWorkAsync(async () =>
        {
            var initial = await _connectorIngestionService.IngestReceivedOrderAsync(
                ThreePLTestData.CreateContext(partnerId, tenantId),
                new ReceiveOrderRequest
                {
                    Intent = ReceiveOrderIntent.OutboundToPartner,
                    ExternalOrderId = externalOrderId
                });
            initial.IsNew.ShouldBeTrue();
            initial.SourceVersion.ShouldBe(1);

            var status = await _connectorIngestionService.IngestStatusUpdateAsync(
                ThreePLTestData.CreateContext(partnerId, tenantId),
                new CanonicalOrderStatusUpdate
                {
                    ExternalOrderId = externalOrderId,
                    Version = 2,
                    NewStatus = CanonicalOrderStatus.InTransit,
                    OccurredAt = _clock.Now
                });
            status.IsNew.ShouldBeTrue();
            status.SourceVersion.ShouldBe(2);
            status.OrderRecordId.ShouldNotBe(initial.OrderRecordId);

            var records = (await _orderRecordRepository.GetListAsync(x => x.SourceOrderId == externalOrderId))
                .OrderBy(x => x.SourceVersion)
                .ToList();

            records.Count.ShouldBe(2);
            records.Select(x => x.SourceOrderId).Distinct().Count().ShouldBe(1);
            records[0].SourceVersion.ShouldBe(1);
            records[1].SourceVersion.ShouldBe(2);
            records[0].SourceSystem.ShouldBe(records[1].SourceSystem);
            records[0].Status.ShouldBe(OrderStatus.Created);
            records[1].Status.ShouldBe(OrderStatus.Paid);
        });
    }

    [Fact]
    public async Task Should_Append_Carrier_Status_Update_As_New_SourceVersion_Not_Duplicate_Order()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        const string externalOrderId = "carrier-versioned-order";
        _carrierTransport.Reset();
        _carrierTransport.StageShipment(CarrierTestData.CreateShipmentRequest(partnerId, tenantId, externalOrderId));

        await WithUnitOfWorkAsync(async () =>
        {
            var initial = await _connectorIngestionService.IngestReceivedOrderAsync(
                CarrierTestData.CreateContext(partnerId, tenantId),
                new ReceiveOrderRequest
                {
                    Intent = ReceiveOrderIntent.CreateShipment,
                    ExternalOrderId = externalOrderId
                });
            initial.IsNew.ShouldBeTrue();
            initial.SourceVersion.ShouldBe(1);

            var status = await _connectorIngestionService.IngestStatusUpdateAsync(
                CarrierTestData.CreateContext(partnerId, tenantId),
                new CanonicalOrderStatusUpdate
                {
                    ExternalOrderId = externalOrderId,
                    Version = 2,
                    NewStatus = CanonicalOrderStatus.InTransit,
                    OccurredAt = _clock.Now
                });
            status.IsNew.ShouldBeTrue();
            status.SourceVersion.ShouldBe(2);

            var records = (await _orderRecordRepository.GetListAsync(x => x.SourceOrderId == externalOrderId))
                .OrderBy(x => x.SourceVersion)
                .ToList();

            records.Count.ShouldBe(2);
            records.Select(x => x.SourceOrderId).Distinct().Count().ShouldBe(1);
            records[0].SourceVersion.ShouldBe(1);
            records[1].SourceVersion.ShouldBe(2);
            records[0].SourceSystem.ShouldBe("connector:carrier:mock-carrier");
        });
    }
}
