using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Connectors;

public interface IThreePLConnector : IPartnerConnector
{
    Task<ConnectorResult<CanonicalInventoryReconcileResult>> ReconcileInventoryAsync(
        ConnectorContext context,
        CanonicalInventoryReconcileRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CanonicalInventoryReconcileRequest
{
    public string WarehouseExternalId { get; init; } = string.Empty;

    public IReadOnlyList<CanonicalInventoryReconcileLine> ReportedOnHand { get; init; } = [];
}
