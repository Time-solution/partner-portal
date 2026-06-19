namespace Zahy.Connectors;

public sealed class CanonicalOrderStatusUpdate
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public long Version { get; init; }

    public CanonicalOrderStatus NewStatus { get; init; }

    public CanonicalPaymentState? PaymentState { get; init; }

    public DateTime OccurredAt { get; init; }
}
