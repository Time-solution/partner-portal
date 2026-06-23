using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
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
    private readonly IConnectorInboundVerifier _inboundVerifier;
    private readonly ConnectorInboundSecurityOptions _securityOptions;

    public ConnectorOrderIngestionService(
        IConnectorRegistry connectorRegistry,
        ICanonicalOrderMapper canonicalOrderMapper,
        IOrderLedgerIngestionService orderLedgerIngestionService,
        IRepository<OrderRecord, Guid> orderRecordRepository,
        IConnectorInboundVerifier inboundVerifier,
        IOptions<ConnectorInboundSecurityOptions> securityOptions)
    {
        _connectorRegistry = connectorRegistry;
        _canonicalOrderMapper = canonicalOrderMapper;
        _orderLedgerIngestionService = orderLedgerIngestionService;
        _orderRecordRepository = orderRecordRepository;
        _inboundVerifier = inboundVerifier;
        _securityOptions = securityOptions.Value;
    }

    [UnitOfWork]
    public virtual async Task<OrderIngestResult> IngestReceivedOrderAsync(
        ConnectorContext context,
        ReceiveOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var connector = _connectorRegistry.Resolve(context.ConnectorCode);

        // Inbound boundary security: verify HMAC over the raw body BEFORE ingestion (gated OFF until a
        // live receive endpoint is wired). Fail closed — reject + log on anything but a fresh, valid signature.
        if (_securityOptions.VerifySignature)
        {
            await VerifyInboundOrThrowAsync(context, connector, request, cancellationToken);
        }

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

    private async Task VerifyInboundOrThrowAsync(
        ConnectorContext context,
        IPartnerConnector connector,
        ReceiveOrderRequest request,
        CancellationToken cancellationToken)
    {
        var registration = await FindRegistrationAsync(context, cancellationToken);
        var secretReference = registration?.SecretReference ?? string.Empty;
        var sourceSystem = ConnectorSourceSystem.Build(connector.Descriptor.Kind, context.ConnectorCode);

        var status = await _inboundVerifier.VerifyAsync(secretReference, sourceSystem, request, cancellationToken);
        if (status == ConnectorSignatureStatus.Valid)
        {
            return;
        }

        Logger.LogWarning(
            "Rejected inbound connector order {ExternalOrderId} on {ConnectorCode} for partner {PartnerId}: {Status}",
            request.ExternalOrderId, context.ConnectorCode, context.PartnerId, status);

        throw new BusinessException(ConnectorErrorCodes.ConnectorSignatureRejected)
            .WithData("ConnectorCode", context.ConnectorCode)
            .WithData("ExternalOrderId", request.ExternalOrderId)
            .WithData("Status", status.ToString());
    }

    private async Task<ConnectorRegistration?> FindRegistrationAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        // Resolved lazily so hosts that never enable inbound verification need not map the registration repo.
        var registrationRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<ConnectorRegistration, Guid>>();
        var queryable = await registrationRepository.GetQueryableAsync();

        if (context.ConnectorRegistrationId.HasValue)
        {
            var id = context.ConnectorRegistrationId.Value;
            return queryable.FirstOrDefault(x => x.Id == id);
        }

        return queryable.FirstOrDefault(x =>
            x.PartnerId == context.PartnerId &&
            x.ConnectorCode == context.ConnectorCode);
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
