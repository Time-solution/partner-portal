namespace Zahy.Connectors;

public sealed class ConnectorContext
{
    public Guid PartnerId { get; init; }

    public Guid TenantId { get; init; }

    public string ConnectorCode { get; init; } = string.Empty;

    public Guid? ConnectorRegistrationId { get; init; }
}

public sealed class ReceiveOrderRequest
{
    public ReceiveOrderIntent Intent { get; init; }

    public string ExternalOrderId { get; init; } = string.Empty;
}

public sealed class OrderActionRequest
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public string? Reason { get; init; }
}

public sealed class CanonicalMenuSyncRequest
{
    public string? OutletExternalId { get; init; }
}
