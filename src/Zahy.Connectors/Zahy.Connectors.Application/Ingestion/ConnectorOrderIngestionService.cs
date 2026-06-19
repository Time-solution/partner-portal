using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Zahy.OrderLedger;

namespace Zahy.Connectors;

public class ConnectorOrderIngestionService : ApplicationService, IConnectorOrderIngestionService
{
    private readonly IConnectorRegistry _connectorRegistry;
    private readonly ICanonicalOrderMapper _canonicalOrderMapper;
    private readonly IOrderLedgerIngestionService _orderLedgerIngestionService;
    private readonly IRepository<OrderRecord, Guid> _orderRecordRepository;

    public ConnectorOrderIngestionService(
        IConnectorRegistry connectorRegistry,
        ICanonicalOrderMapper canonicalOrderMapper,
        IOrderLedgerIngestionService orderLedgerIngestionService,
        IRepository<OrderRecord, Guid> orderRecordRepository)
    {
        _connectorRegistry = connectorRegistry;
        _canonicalOrderMapper = canonicalOrderMapper;
        _orderLedgerIngestionService = orderLedgerIngestionService;
        _orderRecordRepository = orderRecordRepository;
    }

    [UnitOfWork]
    public virtual async Task<OrderIngestResult> IngestReceivedOrderAsync(
        ConnectorContext context,
        ReceiveOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var connector = _connectorRegistry.Resolve(context.ConnectorCode);
        var receiveResult = await connector.ReceiveOrderAsync(context, request, cancellationToken);

        if (!receiveResult.Success || receiveResult.Value == null)
        {
            throw new Volo.Abp.BusinessException(ConnectorErrorCodes.ConnectorReceiveFailed)
                .WithData("ConnectorCode", context.ConnectorCode)
                .WithData("ExternalOrderId", request.ExternalOrderId)
                .WithData("Message", receiveResult.Message ?? "Receive failed.");
        }

        var snapshot = _canonicalOrderMapper.ToSnapshot(receiveResult.Value);
        return await _orderLedgerIngestionService.IngestSnapshotAsync(snapshot, cancellationToken);
    }

    [UnitOfWork]
    public virtual async Task<OrderIngestResult> IngestStatusUpdateAsync(
        ConnectorContext context,
        CanonicalOrderStatusUpdate update,
        CancellationToken cancellationToken = default)
    {
        var connector = _connectorRegistry.Resolve(context.ConnectorCode);
        var updateResult = await connector.UpdateStatusAsync(context, update, cancellationToken);

        if (!updateResult.Success || updateResult.Value == null)
        {
            throw new Volo.Abp.BusinessException(ConnectorErrorCodes.ConnectorStatusUpdateFailed)
                .WithData("ConnectorCode", context.ConnectorCode)
                .WithData("ExternalOrderId", update.ExternalOrderId)
                .WithData("Message", updateResult.Message ?? "Status update failed.");
        }

        var sourceSystem = ConnectorSourceSystem.Build(connector.Descriptor.Kind, context.ConnectorCode);
        var latestRecord = await FindLatestRecordAsync(sourceSystem, update.ExternalOrderId);
        if (latestRecord == null)
        {
            throw new Volo.Abp.BusinessException(ConnectorErrorCodes.ConnectorOrderNotFoundInLedger)
                .WithData("SourceSystem", sourceSystem)
                .WithData("ExternalOrderId", update.ExternalOrderId);
        }

        var latestSnapshot = _canonicalOrderMapper.ToSnapshot(latestRecord);
        var nextSnapshot = _canonicalOrderMapper.ToStatusSnapshot(latestSnapshot, updateResult.Value);
        return await _orderLedgerIngestionService.IngestSnapshotAsync(nextSnapshot, cancellationToken);
    }

    private async Task<OrderRecord?> FindLatestRecordAsync(string sourceSystem, string sourceOrderId)
    {
        var queryable = await _orderRecordRepository.GetQueryableAsync();
        return queryable
            .Where(x =>
                x.SourceSystem == sourceSystem &&
                x.SourceOrderId == sourceOrderId)
            .OrderByDescending(x => x.SourceVersion)
            .FirstOrDefault();
    }
}
