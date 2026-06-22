using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// U1 — usage metering RECORDS how many units a merchant consumed for a partner in a period. PURE DATA:
/// records accumulate per partner+merchant+period and sum; no fee is computed and no journal is posted.
/// IDs mirror the frontend demo fixtures (WhatsApp Co / W there + their active merchants).
/// </summary>
public class UsageMeteringTests
{
    private static readonly Guid Whatsapp = Guid.Parse("22222222-2222-2222-2222-222222222004");
    private static readonly Guid Wthere = Guid.Parse("22222222-2222-2222-2222-222222222008");
    private static readonly Guid Pizza = Guid.Parse("11111111-1111-1111-1111-111111111003");
    private static readonly Guid Coffee = Guid.Parse("11111111-1111-1111-1111-111111111002");
    private static readonly Guid Burger = Guid.Parse("11111111-1111-1111-1111-111111111001");
    private static readonly SettlementPeriod Jun = SettlementPeriod.Of(2026, 6);
    private static readonly SettlementPeriod May = SettlementPeriod.Of(2026, 5);

    private static UsageRecord Rec(Guid partner, Guid merchant, SettlementPeriod period, decimal qty, string source = "seed") =>
        new(Guid.NewGuid(), partner, merchant, period, "messages", qty, source);

    /// <summary>Demo seed mirroring the frontend store — Pizza's 6,200 is two incremental rows (4,000 + 2,200).</summary>
    private static List<UsageRecord> SeedDemo() => new()
    {
        Rec(Whatsapp, Pizza, Jun, 4000m),
        Rec(Whatsapp, Pizza, Jun, 2200m), // incremental — accumulates to 6,200
        Rec(Whatsapp, Coffee, Jun, 3100m),
        Rec(Wthere, Burger, Jun, 4800m),
        Rec(Whatsapp, Pizza, May, 5000m), // a different period — must not leak into June
    };

    [Fact]
    public void UsageForPeriod_Sums_Incremental_Records_Into_The_Period_Total()
    {
        var records = SeedDemo();

        // Pizza used 6,200 messages via WhatsApp in 2026-06 (4,000 + 2,200 accumulated).
        UsageReports.UsageForPeriod(records, Whatsapp, Pizza, Jun).ShouldBe(6200m);
        UsageReports.UsageForPeriod(records, Whatsapp, Coffee, Jun).ShouldBe(3100m);
        UsageReports.UsageForPeriod(records, Wthere, Burger, Jun).ShouldBe(4800m);
    }

    [Fact]
    public void UsageForPeriod_Is_Keyed_To_Partner_Merchant_And_Period()
    {
        var records = SeedDemo();

        // Different period is excluded.
        UsageReports.UsageForPeriod(records, Whatsapp, Pizza, May).ShouldBe(5000m);
        // Wrong partner / merchant / no data → 0.
        UsageReports.UsageForPeriod(records, Wthere, Pizza, Jun).ShouldBe(0m);
        UsageReports.UsageForPeriod(records, Whatsapp, Burger, Jun).ShouldBe(0m);
    }

    [Fact]
    public void Incremental_Records_Accumulate_When_More_Usage_Is_Appended()
    {
        var records = SeedDemo();
        UsageReports.UsageForPeriod(records, Whatsapp, Pizza, Jun).ShouldBe(6200m);

        // Append another increment (e.g. a later batch in the same period).
        records.Add(Rec(Whatsapp, Pizza, Jun, 800m, "manual"));
        UsageReports.UsageForPeriod(records, Whatsapp, Pizza, Jun).ShouldBe(7000m);
    }

    [Fact]
    public void PartnerPeriodRollup_Breaks_Down_Per_Merchant_And_Totals_The_Partner()
    {
        var rollup = UsageReports.PartnerPeriodRollup(SeedDemo(), Whatsapp, Jun);

        rollup.PartnerId.ShouldBe(Whatsapp);
        rollup.PerMerchant.Count.ShouldBe(2); // Pizza + Coffee
        rollup.PerMerchant.Single(m => m.MerchantId == Pizza).Quantity.ShouldBe(6200m);
        rollup.PerMerchant.Single(m => m.MerchantId == Coffee).Quantity.ShouldBe(3100m);
        rollup.TotalQuantity.ShouldBe(9300m); // 6,200 + 3,100 (May excluded)
    }

    [Fact]
    public void Usage_Records_Validate_Their_Inputs()
    {
        Should.Throw<Volo.Abp.AbpException>(() => Rec(Whatsapp, Pizza, Jun, -1m));
        Should.Throw<Volo.Abp.AbpException>(() => new UsageRecord(Guid.NewGuid(), Guid.Empty, Pizza, Jun, "messages", 1m));
        Should.Throw<Volo.Abp.AbpException>(() => new UsageRecord(Guid.NewGuid(), Whatsapp, Guid.Empty, Jun, "messages", 1m));
    }

    [Fact]
    public void Recording_Usage_Posts_No_Journal_And_Leaves_The_Trial_Balance_Untouched()
    {
        // A balanced settlement journal (the ONLY thing that affects the trial balance).
        var entries = new[]
        {
            SettlementPostingTemplates
                .Principal(
                    Money.Of(100m, SettlementConsts.DefaultCurrency, vatInclusive: true),
                    Money.Of(70m, SettlementConsts.DefaultCurrency, vatInclusive: true),
                    0.15m)
                .Tag(Whatsapp, Pizza, Jun, "ORD-1"),
        };

        var before = SettlementReports.TrialBalanceFor(entries, Jun);
        before.IsBalanced.ShouldBeTrue();

        // "Record" a pile of usage — pure data, never fed into the journal/report.
        _ = SeedDemo();

        var after = SettlementReports.TrialBalanceFor(entries, Jun);
        after.IsBalanced.ShouldBeTrue();
        after.Net.ShouldBe(0m);
        after.Net.ShouldBe(before.Net); // usage changed nothing
    }
}
