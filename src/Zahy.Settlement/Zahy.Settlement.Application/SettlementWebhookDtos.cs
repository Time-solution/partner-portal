using System;

namespace Zahy.Settlement;

/// <summary>
/// An inbound settlement webhook as handed to the engine. The adapter maps partner/book routing and
/// parses the component amounts; the engine resolves the signing secret server-side (never from the
/// caller) then verifies, dedupes, routes, allocates, and logs. Payload is the raw signed JSON.
/// </summary>
public sealed record InboundSettlementWebhook
{
    public SettlementBook Book { get; init; }

    public Guid PartnerId { get; init; }

    /// <summary>False when the adapter could not map the partner/book — routes to quarantine.</summary>
    public bool PartnerKnown { get; init; } = true;

    public string ExternalEventId { get; init; } = string.Empty;

    public string Payload { get; init; } = string.Empty;

    public string? Signature { get; init; }

    public long UnixTimestamp { get; init; }

    public DateTime ReceivedAt { get; init; }

    public SettlementAllocationInput Allocation { get; init; } = new();
}

public sealed record SettlementWebhookAck
{
    /// <summary>True ⇒ the HTTP adapter returns 2xx and processing continues asynchronously.</summary>
    public bool Accepted { get; init; }

    public WebhookSignatureStatus SignatureStatus { get; init; }

    public string? Reason { get; init; }
}

public sealed record SettlementIngestResult
{
    public SettlementEventOutcome Outcome { get; init; }

    public Guid? SettlementCaseId { get; init; }

    public SettlementCaseState? State { get; init; }

    public WebhookSignatureStatus SignatureStatus { get; init; }

    public string? Reason { get; init; }
}
