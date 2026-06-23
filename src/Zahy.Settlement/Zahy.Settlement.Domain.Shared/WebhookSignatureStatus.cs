namespace Zahy.Settlement;

/// <summary>Result of verifying an inbound settlement webhook's HMAC signature.</summary>
public enum WebhookSignatureStatus
{
    /// <summary>No signature header was present — treated as hostile.</summary>
    Missing = 0,

    /// <summary>A signature was present but did not match.</summary>
    Invalid = 1,

    /// <summary>Signature verified against the partner's signing secret.</summary>
    Valid = 2,

    /// <summary>Signature matched but the timestamp is outside the replay window — treated as hostile.</summary>
    Stale = 3
}
