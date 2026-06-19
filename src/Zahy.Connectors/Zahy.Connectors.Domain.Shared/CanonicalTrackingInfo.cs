namespace Zahy.Connectors;

public sealed class CanonicalTrackingInfo
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public CanonicalOrderStatus Status { get; init; }

    public string? TrackingNumber { get; init; }

    public string? CarrierName { get; init; }

    public DateTime LastUpdatedAt { get; init; }
}
