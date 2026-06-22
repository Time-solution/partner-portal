using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// U1 guardrails — the auto-feed is a SEAM ONLY (not wired): <see cref="UsageFeedSeam.IsWired"/> is false
/// and the auto-push path throws, proving no live feed silently ingests usage. All money flags stay OFF —
/// U1 records data only, it bills/posts nothing (that is the gated U3 phase).
/// </summary>
public class UsageFeedSeamTests
{
    [Fact]
    public void Usage_Auto_Feed_Is_Not_Wired()
    {
        UsageFeedSeam.IsWired.ShouldBeFalse();
    }

    [Fact]
    public void Auto_Push_Path_Throws_Because_It_Is_Not_Implemented()
    {
        var signals = new List<UsageFeedSignal>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), SettlementPeriod.Of(2026, 6), "messages", 1200m, "partner-feed"),
        };

        Should.Throw<NotSupportedException>(() => UsageFeedSeam.AutoPush(signals));
    }

    [Fact]
    public void Usage_Does_Not_Flip_Any_Money_Flag()
    {
        // U1 is pure data — none of the money/posting flags are touched and all remain OFF.
        var options = new SettlementEngineOptions();
        options.PostingEnabled.ShouldBeFalse();
        options.DisbursementEnabled.ShouldBeFalse();
        options.LiveProviderEnabled.ShouldBeFalse();
        options.BankRegistryLiveChartEnabled.ShouldBeFalse();
    }
}
