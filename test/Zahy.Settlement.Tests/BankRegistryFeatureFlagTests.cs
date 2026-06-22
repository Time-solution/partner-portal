using System;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Track B guardrails — the live-chart flag stays OFF (registry/routing are mock/compute-only and the
/// production chart is not mutated), and the gateway auto-route is a SEAM ONLY (not wired).
/// </summary>
public class BankRegistryFeatureFlagTests
{
    [Fact]
    public void Live_Chart_Change_Is_Off_By_Default()
    {
        new SettlementEngineOptions().BankRegistryLiveChartEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Other_Money_Flags_Remain_Off_Too()
    {
        var options = new SettlementEngineOptions();
        options.PostingEnabled.ShouldBeFalse();
        options.DisbursementEnabled.ShouldBeFalse();
        options.LiveProviderEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Gateway_Auto_Route_Is_Not_Wired()
    {
        GatewayAutoRouteSeam.IsWired.ShouldBeFalse();
    }

    [Fact]
    public void Gateway_Seam_Can_Resolve_A_Mapping_But_Auto_Reconcile_Throws()
    {
        var banks = new[]
        {
            ("1101", (string?)"gw-ncb-001"),
            ("1102", (string?)"gw-rajhi-002"),
        };

        // Pure lookup is reachable (proves the mapping seam exists)…
        GatewayAutoRouteSeam.ResolveBankCode("gw-rajhi-002", banks).ShouldBe("1102");
        GatewayAutoRouteSeam.ResolveBankCode("unknown", banks).ShouldBeNull();
        GatewayAutoRouteSeam.ResolveBankCode(null, banks).ShouldBeNull();

        // …but the actual auto-route/auto-reconcile is intentionally NOT implemented.
        Should.Throw<NotSupportedException>(() => GatewayAutoRouteSeam.AutoReconcile("gw-ncb-001", banks));
    }
}
