using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// U5 — GRADUATED (marginal) volume tiers on the overage. Each bracket's units are charged at that
/// bracket's rate. Verifies back-compat (no tiers == flat U3), the worked graduated numbers, resale
/// buy/sell + margin, subscription fee tiers, routing to the EXISTING templates, and entity validation.
/// CHOSEN MODEL: graduated/marginal (standard for usage billing) — NOT single-bracket volume pricing.
/// </summary>
public class UsageVolumeTierTests
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222004");
    private static readonly SettlementPeriod June = SettlementPeriod.Of(2026, 6); // 30 days

    private static UsagePackage Resale(decimal included, decimal baseBuy, decimal baseSell, decimal overBuy, decimal overSell) =>
        new(Guid.NewGuid(), PartnerId, "Resale", "messages", UsagePackageMode.Resale,
            "SAR", included, baseBuy, baseSell, overBuy, overSell, ActivationFeePayer.Merchant);

    private static UsagePackage Subscription(decimal included, decimal baseFee, decimal overFee, ActivationFeePayer payer) =>
        new(Guid.NewGuid(), PartnerId, "Sub", "messages", UsagePackageMode.Subscription,
            "SAR", included, 0m, baseFee, 0m, overFee, payer);

    // ---- Back-compat: no tiers == flat U3 ----------------------------------------------------

    [Fact]
    public void No_Tiers_Is_Identical_To_Flat_U3()
    {
        var pkg = Resale(included: 5000m, baseBuy: 0m, baseSell: 0m, overBuy: 0.03m, overSell: 0.05m);
        pkg.Tiers.Count.ShouldBe(0);

        var r = UsageBillingCalculator.Compute(pkg, usage: 16200m, June); // excess 11200
        r.Sell!.OverageInclusive.ShouldBe(560m); // 11200 × 0.05 (flat)
        r.Sell.Tiers.Count.ShouldBe(0);          // no tier detail when flat
        r.Buy!.OverageInclusive.ShouldBe(336m);  // 11200 × 0.03
    }

    // ---- Graduated worked example -------------------------------------------------------------

    [Fact]
    public void Graduated_Tiers_Charge_Each_Bracket_At_Its_Own_Rate()
    {
        // included 5,000; used 16,200 → overage 11,200.
        // Tiers (overage units from 0): 0–10,000 @ sell 0.05 ; 10,000+ @ sell 0.04 (open-ended).
        var pkg = Resale(included: 5000m, baseBuy: 0m, baseSell: 0m, overBuy: 0.99m, overSell: 0.99m); // flat ignored
        pkg.SetTiers(new[]
        {
            new UsagePackageTier { FromQuantity = 0m, ToQuantity = 10000m, BuyRate = 0.03m, SellRate = 0.05m },
            new UsagePackageTier { FromQuantity = 10000m, ToQuantity = null, BuyRate = 0.025m, SellRate = 0.04m },
        });

        var r = UsageBillingCalculator.Compute(pkg, usage: 16200m, June);

        // first 10,000 × 0.05 = 500 + next 1,200 × 0.04 = 48 → 548.
        r.Sell!.OverageInclusive.ShouldBe(548m);
        r.Sell.Tiers.Count.ShouldBe(2);
        r.Sell.Tiers[0].Units.ShouldBe(10000m);
        r.Sell.Tiers[0].AmountInclusive.ShouldBe(500m);
        r.Sell.Tiers[1].Units.ShouldBe(1200m);   // open-ended top tier takes the remainder
        r.Sell.Tiers[1].AmountInclusive.ShouldBe(48m);

        // Buy side: 10,000 × 0.03 = 300 + 1,200 × 0.025 = 30 → 330; margin 548 − 330 = 218.
        r.Buy!.OverageInclusive.ShouldBe(330m);
        r.MarginInclusive.ShouldBe(218m);

        // Routes to the EXISTING Principal template, balanced, compute-only.
        r.Journal.Mode.ShouldBe(ParticipationMode.Principal);
        r.Journal.IsBalanced.ShouldBeTrue();
        r.Journal.TrialBalanceNet.ShouldBe(0m);
        r.Journal.LineFor(SettlementAccountCode.ArMerchant, EntryDirection.Debit)!.Amount.Amount.ShouldBe(548m);
        r.Journal.LineFor(SettlementAccountCode.ApPartner, EntryDirection.Credit)!.Amount.Amount.ShouldBe(330m);
    }

    [Fact]
    public void Usage_Within_First_Tier_Only_Charges_That_Bracket()
    {
        var pkg = Resale(included: 5000m, baseBuy: 0m, baseSell: 0m, overBuy: 0.03m, overSell: 0.05m);
        pkg.SetTiers(new[]
        {
            new UsagePackageTier { FromQuantity = 0m, ToQuantity = 10000m, BuyRate = 0.03m, SellRate = 0.05m },
            new UsagePackageTier { FromQuantity = 10000m, ToQuantity = null, BuyRate = 0.025m, SellRate = 0.04m },
        });

        var r = UsageBillingCalculator.Compute(pkg, usage: 11000m, June); // overage 6,000 — all in tier 1
        r.Sell!.OverageInclusive.ShouldBe(300m); // 6000 × 0.05
        r.Sell.Tiers.Count.ShouldBe(1);
        r.Sell.Tiers[0].Units.ShouldBe(6000m);
    }

    // ---- Subscription fee tiers ---------------------------------------------------------------

    [Fact]
    public void Subscription_Tiers_Use_The_Fee_Rate_And_Route_To_Fee()
    {
        var pkg = Subscription(included: 5000m, baseFee: 0m, overFee: 0.99m, ActivationFeePayer.Partner);
        pkg.SetTiers(new[]
        {
            new UsagePackageTier { FromQuantity = 0m, ToQuantity = 10000m, BuyRate = 9m /*ignored*/, SellRate = 0.05m },
            new UsagePackageTier { FromQuantity = 10000m, ToQuantity = null, BuyRate = 9m, SellRate = 0.04m },
        });

        // Subscription forces the buy side to 0 on every tier.
        pkg.Tiers.ShouldAllBe(t => t.BuyRate == 0m);

        var r = UsageBillingCalculator.Compute(pkg, usage: 16200m, June); // overage 11,200
        r.Fee!.OverageInclusive.ShouldBe(548m); // 500 + 48
        r.Fee.Tiers.Count.ShouldBe(2);
        r.Payer.ShouldBe(ActivationFeePayer.Partner);
        r.Journal.Mode.ShouldBe(ParticipationMode.SubscriptionFee);
        r.Journal.LineFor(SettlementAccountCode.ArPartner, EntryDirection.Debit)!.Amount.Amount.ShouldBe(548m);
        r.Journal.TrialBalanceNet.ShouldBe(0m);
    }

    // ---- Entity validation --------------------------------------------------------------------

    [Fact]
    public void First_Tier_Must_Start_At_Zero()
    {
        var pkg = Resale(5000m, 0m, 0m, 0.03m, 0.05m);
        var ex = Should.Throw<BusinessException>(() => pkg.SetTiers(new[]
        {
            new UsagePackageTier { FromQuantity = 100m, ToQuantity = null, SellRate = 0.05m },
        }));
        ex.Code.ShouldBe(PartnerCatalogErrorCodes.InvalidUsagePackageTier);
    }

    [Fact]
    public void Tiers_Must_Be_Contiguous()
    {
        var pkg = Resale(5000m, 0m, 0m, 0.03m, 0.05m);
        Should.Throw<BusinessException>(() => pkg.SetTiers(new[]
        {
            new UsagePackageTier { FromQuantity = 0m, ToQuantity = 10000m, SellRate = 0.05m },
            new UsagePackageTier { FromQuantity = 12000m, ToQuantity = null, SellRate = 0.04m }, // gap
        }));
    }

    [Fact]
    public void Open_Ended_Tier_Must_Be_Last()
    {
        var pkg = Resale(5000m, 0m, 0m, 0.03m, 0.05m);
        Should.Throw<BusinessException>(() => pkg.SetTiers(new[]
        {
            new UsagePackageTier { FromQuantity = 0m, ToQuantity = null, SellRate = 0.05m },
            new UsagePackageTier { FromQuantity = 10000m, ToQuantity = null, SellRate = 0.04m },
        }));
    }

    [Fact]
    public void Negative_Tier_Rate_Is_Rejected()
    {
        var pkg = Resale(5000m, 0m, 0m, 0.03m, 0.05m);
        Should.Throw<BusinessException>(() => pkg.SetTiers(new[]
        {
            new UsagePackageTier { FromQuantity = 0m, ToQuantity = null, SellRate = -0.01m },
        }));
    }

    [Fact]
    public void Setting_Empty_Tiers_Clears_Back_To_Flat()
    {
        var pkg = Resale(5000m, 0m, 0m, 0.03m, 0.05m);
        pkg.SetTiers(new[] { new UsagePackageTier { FromQuantity = 0m, ToQuantity = null, SellRate = 0.05m } });
        pkg.Tiers.Count.ShouldBe(1);
        pkg.SetTiers(null);
        pkg.Tiers.Count.ShouldBe(0);
        pkg.TiersJson.ShouldBeNull();
    }
}
