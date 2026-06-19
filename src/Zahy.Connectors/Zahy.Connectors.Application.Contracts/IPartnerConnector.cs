using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Connectors;

public interface IPartnerConnector
{
    ConnectorDescriptor Descriptor { get; }

    Task<ConnectorResult<CanonicalMenuSyncResult>> SyncMenuAsync(
        ConnectorContext context,
        CanonicalMenuSyncRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectorResult<CanonicalOutlet>> MapBranchAsync(
        ConnectorContext context,
        CanonicalOutlet outlet,
        CancellationToken cancellationToken = default);

    Task<ConnectorResult<CanonicalOrder>> ReceiveOrderAsync(
        ConnectorContext context,
        ReceiveOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectorResult<CanonicalAcceptResult>> AcceptOrderAsync(
        ConnectorContext context,
        OrderActionRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectorResult<CanonicalAcceptResult>> RejectOrderAsync(
        ConnectorContext context,
        OrderActionRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectorResult<CanonicalOrderStatusUpdate>> UpdateStatusAsync(
        ConnectorContext context,
        CanonicalOrderStatusUpdate update,
        CancellationToken cancellationToken = default);

    Task<ConnectorResult<CanonicalTrackingInfo>> GetTrackingAsync(
        ConnectorContext context,
        OrderActionRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectorResult<CanonicalReturnRequest>> HandleReturnAsync(
        ConnectorContext context,
        CanonicalReturnRequest request,
        CancellationToken cancellationToken = default);
}
