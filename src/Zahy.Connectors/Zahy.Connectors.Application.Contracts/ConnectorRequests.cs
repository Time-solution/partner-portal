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

    /// <summary>
    /// The exact raw request body the partner signed. HMAC verification runs over THIS string (never a
    /// re-serialized object) so the signature matches byte-for-byte. Optional while verification is gated OFF.
    /// </summary>
    public string RawPayload { get; init; } = string.Empty;

    /// <summary>The caller-supplied HMAC header (e.g. "sha256=…"). The secret is resolved server-side, not here.</summary>
    public string? Signature { get; init; }

    /// <summary>Unix seconds the payload was signed; checked against the replay window.</summary>
    public long UnixTimestamp { get; init; }

    /// <summary>Per-delivery nonce / event id used to reject replays. Falls back to ExternalOrderId when absent.</summary>
    public string? Nonce { get; init; }
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
