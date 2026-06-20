namespace Zahy.Settlement;

/// <summary>What the engine did with an inbound settlement webhook (recorded in the append-only event log).</summary>
public enum SettlementEventOutcome
{
    /// <summary>Rejected before processing (bad/absent signature or malformed payload).</summary>
    Rejected = 1,

    /// <summary>Could not be routed to a known partner/book — held for manual review, never auto-processed.</summary>
    Quarantined = 2,

    /// <summary>Processed: a balanced journal was posted and the case moved Collected → Allocated.</summary>
    Allocated = 3,

    /// <summary>A replay of an already-processed event — no second journal or transition.</summary>
    DuplicateIgnored = 4
}
