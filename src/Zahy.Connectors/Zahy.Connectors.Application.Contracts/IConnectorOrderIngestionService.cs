using System.Threading;
using System.Threading.Tasks;
using Zahy.OrderLedger;

namespace Zahy.Connectors;

public interface IConnectorOrderIngestionService
{
    Task<OrderIngestResult> IngestReceivedOrderAsync(
        ConnectorContext context,
        ReceiveOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderIngestResult> IngestStatusUpdateAsync(
        ConnectorContext context,
        CanonicalOrderStatusUpdate update,
        CancellationToken cancellationToken = default);
}
