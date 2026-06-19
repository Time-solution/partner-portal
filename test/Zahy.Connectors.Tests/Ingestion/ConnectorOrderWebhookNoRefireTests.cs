using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp;
using Volo.Abp.Testing;
using Xunit;
using Zahy.OrderLedger;
using Zahy.Webhooks;

namespace Zahy.Connectors;

public abstract class ZahyConnectorsWebhookTestBase : AbpIntegratedTest<ZahyConnectorsWebhookIntegrationTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected virtual async Task WithUnitOfWorkAsync(Func<Task> action)
    {
        var uowManager = GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        await action();
        await uow.CompleteAsync();
    }
}

public class ConnectorOrderWebhookNoRefireTests : ZahyConnectorsWebhookTestBase
{
    private readonly IConnectorOrderIngestionService _connectorIngestionService;
    private readonly IRepository<OrderRecord, Guid> _orderRecordRepository;
    private readonly IRepository<WebhookOutboxMessage, Guid> _outboxRepository;
    private readonly InMemoryAggregatorConnectorTransport _transport;
    private readonly ConnectorsTestCurrentPartner _currentPartner;
    private readonly IClock _clock;

    public ConnectorOrderWebhookNoRefireTests()
    {
        _connectorIngestionService = GetRequiredService<IConnectorOrderIngestionService>();
        _orderRecordRepository = GetRequiredService<IRepository<OrderRecord, Guid>>();
        _outboxRepository = GetRequiredService<IRepository<WebhookOutboxMessage, Guid>>();
        _transport = GetRequiredService<InMemoryAggregatorConnectorTransport>();
        _currentPartner = GetRequiredService<ConnectorsTestCurrentPartner>();
        _clock = GetRequiredService<IClock>();
    }

    [Fact]
    public async Task Duplicate_Aggregator_Receive_Does_Not_Create_Duplicate_Ledger_Or_Outbox_Entry()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _currentPartner.Id = partnerId;
        _transport.Reset();
        _transport.EnqueueOrder(AggregatorTestData.CreateInboundOrder(
            partnerId, tenantId, "full-chain-dup-order", _clock.Now));

        await WithUnitOfWorkAsync(async () =>
        {
            var first = await _connectorIngestionService.IngestReceivedOrderAsync(
                AggregatorTestData.CreateContext(partnerId, tenantId),
                AggregatorTestData.CreateReceiveRequest("full-chain-dup-order"));
            first.IsNew.ShouldBeTrue();

            var ledgerAfterFirst = await _orderRecordRepository.GetListAsync();
            var outboxAfterFirst = await _outboxRepository.GetListAsync();
            ledgerAfterFirst.Count.ShouldBe(1);
            outboxAfterFirst.Count.ShouldBeGreaterThan(0);

            var second = await _connectorIngestionService.IngestReceivedOrderAsync(
                AggregatorTestData.CreateContext(partnerId, tenantId),
                AggregatorTestData.CreateReceiveRequest("full-chain-dup-order"));
            second.IsNew.ShouldBeFalse();

            var ledgerAfterSecond = await _orderRecordRepository.GetListAsync();
            var outboxAfterSecond = await _outboxRepository.GetListAsync();

            ledgerAfterSecond.Count.ShouldBe(ledgerAfterFirst.Count);
            outboxAfterSecond.Count.ShouldBe(outboxAfterFirst.Count);
            ledgerAfterSecond.Count(x =>
                x.SourceSystem == "connector:aggregator:mock-aggregator" &&
                x.SourceOrderId == "full-chain-dup-order" &&
                x.SourceVersion == 1).ShouldBe(1);
        });
    }
}
