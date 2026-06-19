using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Connectors;

public interface ICarrierConnector : IPartnerConnector
{
    Task<ConnectorResult<CanonicalShipmentRate>> GetRateAsync(
        ConnectorContext context,
        CarrierRateRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectorResult<CanonicalShipmentLabel>> CreateLabelAsync(
        ConnectorContext context,
        CarrierLabelRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CarrierRateRequest
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public decimal WeightKg { get; init; }

    public string DestinationCity { get; init; } = string.Empty;
}

public sealed class CarrierLabelRequest
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public string ServiceLevel { get; init; } = string.Empty;
}
