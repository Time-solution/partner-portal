using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// U1 scope — usage carries no money, but it is still row-scoped (respecting the existing RBAC shape):
/// accountant/admin see ALL usage; a partner sees ONLY their own merchants' usage; a merchant sees ONLY
/// their own usage. No cross-entity leakage.
/// </summary>
public class UsageScopeTests
{
    private static readonly Guid PartnerA = Guid.NewGuid();
    private static readonly Guid PartnerB = Guid.NewGuid();
    private static readonly Guid MerchantX = Guid.NewGuid();
    private static readonly Guid MerchantY = Guid.NewGuid();
    private static readonly SettlementPeriod Jun = SettlementPeriod.Of(2026, 6);

    private static UsageRecord Rec(Guid partner, Guid merchant, decimal qty) =>
        new(Guid.NewGuid(), partner, merchant, Jun, "messages", qty, "seed");

    private static UsageRecord[] All() => new[]
    {
        Rec(PartnerA, MerchantX, 100m),
        Rec(PartnerA, MerchantY, 200m),
        Rec(PartnerB, MerchantX, 300m),
        Rec(PartnerB, MerchantY, 400m),
    };

    [Fact]
    public void Accountant_And_Admin_See_All_Usage()
    {
        var scoped = UsageReports.Scope(All(), UsageViewer.Platform());
        scoped.Count.ShouldBe(4);
    }

    [Fact]
    public void Partner_Sees_Only_Their_Own_Merchants_Usage()
    {
        var scoped = UsageReports.Scope(All(), UsageViewer.Partner(PartnerA));

        scoped.Count.ShouldBe(2);
        scoped.ShouldAllBe(r => r.PartnerId == PartnerA);
        scoped.ShouldNotContain(r => r.PartnerId == PartnerB);
    }

    [Fact]
    public void Merchant_Sees_Only_Their_Own_Usage_Across_Partners()
    {
        var scoped = UsageReports.Scope(All(), UsageViewer.Merchant(MerchantX));

        scoped.Count.ShouldBe(2);
        scoped.ShouldAllBe(r => r.MerchantId == MerchantX);
        scoped.ShouldNotContain(r => r.MerchantId == MerchantY);
    }

    [Fact]
    public void Scope_Composes_With_The_Read_Model_Without_Cross_Entity_Leakage()
    {
        // A partner's rollup, computed over only the rows they're allowed to see.
        var visible = UsageReports.Scope(All(), UsageViewer.Partner(PartnerA));
        var rollup = UsageReports.PartnerPeriodRollup(visible, PartnerA, Jun);

        rollup.TotalQuantity.ShouldBe(300m); // 100 (X) + 200 (Y) — none of Partner B's 700
        rollup.PerMerchant.Count.ShouldBe(2);
    }
}
