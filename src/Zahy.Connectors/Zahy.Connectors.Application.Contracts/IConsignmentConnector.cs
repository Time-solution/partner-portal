using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Connectors;

/// <summary>
/// noon JUMP "Fulfilled by" shape. Builds on the existing 3PL connector pattern
/// (<see cref="IThreePLConnector"/>) — reusing its fulfilment + inventory-reconcile rails — and adds
/// the one new element: inbound stock CUSTODY (an ASN / stock transfer from merchant → partner warehouse).
/// </summary>
public interface IConsignmentConnector : IThreePLConnector
{
    Task<ConnectorResult<CanonicalStockTransferResult>> ReceiveStockTransferAsync(
        ConnectorContext context,
        CanonicalStockTransferRequest request,
        CancellationToken cancellationToken = default);
}
