using System;
using System.Collections.Generic;
using System.Linq;

namespace Zahy.Settlement;

/// <summary>
/// One usage signal a future partner feed WOULD push (partner+merchant+period and the consumed units).
/// SEAM payload only — constructing it computes/persists nothing.
/// </summary>
public sealed record UsageFeedSignal(
    Guid PartnerId,
    Guid MerchantId,
    SettlementPeriod Period,
    string UnitLabel,
    decimal Quantity,
    string Source);

/// <summary>
/// SEAM ONLY — documented placeholder for the GATED usage auto-feed phase. NOT WIRED.
///
/// In a later phase a real usage feed (partner API / gateway / metering pipeline) WOULD push
/// <see cref="UsageFeedSignal"/>s in here, which would be appended as <see cref="UsageRecord"/>s
/// (still pure data — no billing). Today U1 only RECORDS usage that is seeded or entered manually.
///
/// Mirrors the Track B <see cref="GatewayAutoRouteSeam"/> pattern: <see cref="IsWired"/> is always
/// false and <see cref="AutoPush"/> intentionally throws, proving no code path silently ingests a
/// live feed. Do NOT implement a real feed here — that is the separately-gated phase.
/// </summary>
public static class UsageFeedSeam
{
    /// <summary>Whether the auto-feed seam is wired to a live usage source. Always false until the gated phase.</summary>
    public const bool IsWired = false;

    /// <summary>
    /// NOT IMPLEMENTED — the gated phase wires the partner usage feed → append usage records here.
    /// Throwing keeps the seam honest: the auto-push path never runs until it is deliberately enabled.
    /// </summary>
    public static IReadOnlyList<UsageRecord> AutoPush(IEnumerable<UsageFeedSignal> signals)
    {
        _ = signals?.ToList();
        throw new NotSupportedException(
            "Usage auto-feed is a SEAM ONLY and is not wired. A real partner/gateway usage feed is " +
            "enabled in the separately-gated phase. Use seeded or manual usage records.");
    }
}
