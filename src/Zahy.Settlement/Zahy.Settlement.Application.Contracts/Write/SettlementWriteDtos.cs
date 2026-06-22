using System;

namespace Zahy.Settlement.Write;

/// <summary>Request to trigger an append-only reversal of an existing settlement case.</summary>
public class TriggerReversalRequest
{
    /// <summary>The settlement case to reverse.</summary>
    public Guid OriginalSettlementCaseId { get; set; }

    /// <summary>
    /// Operator justification. Required, minimum 10 characters — validated in the domain so a bad
    /// reason surfaces as <see cref="SettlementReversalErrorCodes.ReasonRequired"/> (Zahy.Settlement:062).
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Optional caller-supplied idempotency key. When omitted, a deterministic key is derived from
    /// the original case so repeated triggers without a key still resolve to the same reversal.
    /// </summary>
    public string? IdempotencyKey { get; set; }
}
