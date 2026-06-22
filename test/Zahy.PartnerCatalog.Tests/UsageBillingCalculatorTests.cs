using System;
using Shouldly;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// U3 — usage billing calculation (compute-only). Verifies the amount formula, both-mode routing to the
/// EXISTING Principal / Fee templates, proration, VAT split and that NOTHING is posted (flags OFF).
/// </summary>
public class UsageBillingCalculatorTests
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222004");
    private static readonly SettlementPeriod June = SettlementPeriod.Of(2026, 6); // 30 days

    private static UsagePackage Resale(decimal included, decimal baseBuy, decimal baseSell, decimal overBuy, decimal overSell) =>
        new(Guid.NewGuid(), PartnerId, "Resale", "messages", UsagePackageMode.Resale,
            "SAR", included, baseBuy, baseSell, overBuy, overSell, ActivationFeePayer.Merchant);

    private static UsagePackage Subscription(decimal included, decimal baseFee, decimal overFee, ActivationFeePayer payer) =>
        new(Guid.NewGuid(), PartnerId, "Sub", "messages", UsagePackageMode.Subscription,
            "SAR", included, 0m, baseFee, 0m, overFee, payer);

    // ---- amount = base + max(0, usage − included) × overage ----------------------------------

    [Fact]
    public void Usage_Under_Included_Charges_Base_Only()
    {
        var pkg = Subscription(included: 5000m, baseFee: 149m, overFee: 0.03m, ActivationFeePayer.Merchant);
        var r = UsageBillingCalculator.Compute(pkg, usage: 3000m, June);
        r.Fee!.OverageExcess.ShouldBe(0m);
        r.Fee.OverageInclusive.ShouldBe(0m);
        r.Fee.TotalInclusive.ShouldBe(149m);
    }

    [Fact]
    public void Usage_Over_Included_Adds_Overage_Times_Excess()
    {
        var pkg = Subscription(included: 5000m, baseFee: 149m, overFee: 0.03m, ActivationFeePayer.Merchant);
        var r = UsageBillingCalculator.Compute(pkg, usage: 6200m, June);
        r.Fee!.OverageExcess.ShouldBe(1200m);
        r.Fee.OverageInclusive.ShouldBe(36m); // 1200 × 0.03
        r.Fee.TotalInclusive.ShouldBe(185m);  // 149 + 36
    }

    [Fact]
    public void Pure_Per_Use_Is_Rate_Times_Usage()
    {
        var pkg = Subscription(included: 0m, baseFee: 0m, overFee: 0.05m, ActivationFeePayer.Merchant);
        var r = UsageBillingCalculator.Compute(pkg, usage: 1000m, June);
        r.Fee!.TotalInclusive.ShouldBe(50m); // 0 + 1000 × 0.05
    }

    // ---- RESALE: buy + sell + margin, routes to Principal -------------------------------------

    [Fact]
    public void Resale_Computes_Buy_Sell_Margin_And_Routes_To_Principal()
    {
        var pkg = Resale(included: 10000m, baseBuy: 200m, baseSell: 300m, overBuy: 0.02m, overSell: 0.05m);
        var r = UsageBillingCalculator.Compute(pkg, usage: 12000m, June); // excess 2000

        r.Buy!.TotalInclusive.ShouldBe(240m);  // 200 + 2000×0.02
        r.Sell!.TotalInclusive.ShouldBe(400m);  // 300 + 2000×0.05
        r.MarginInclusive.ShouldBe(160m);       // 400 − 240

        r.Journal.Mode.ShouldBe(ParticipationMode.Principal);
        r.Journal.IsBalanced.ShouldBeTrue();
        r.Journal.TrialBalanceNet.ShouldBe(0m);
        // sell leg: 1200 / 4100 / 2200 ; buy leg: 5100 / 1300 / 2100
        r.Journal.LineFor(SettlementAccountCode.ArMerchant, EntryDirection.Debit)!.Amount.Amount.ShouldBe(400m);
        r.Journal.LineFor(SettlementAccountCode.ApPartner, EntryDirection.Credit)!.Amount.Amount.ShouldBe(240m);
        r.Journal.LineFor(SettlementAccountCode.PartnerCogs, EntryDirection.Debit).ShouldNotBeNull();
        r.Journal.LineFor(SettlementAccountCode.ResaleRevenue, EntryDirection.Credit).ShouldNotBeNull();
    }

    // ---- SUBSCRIPTION: fee + payer, routes to Fee --------------------------------------------

    [Fact]
    public void Subscription_Computes_Fee_And_Routes_To_Fee_Template_With_Payer()
    {
        var merchantPaid = UsageBillingCalculator.Compute(
            Subscription(5000m, 149m, 0.03m, ActivationFeePayer.Merchant), usage: 6200m, June);
        merchantPaid.Journal.Mode.ShouldBe(ParticipationMode.SubscriptionFee);
        merchantPaid.Payer.ShouldBe(ActivationFeePayer.Merchant);
        merchantPaid.Journal.LineFor(SettlementAccountCode.ArMerchant, EntryDirection.Debit)!.Amount.Amount.ShouldBe(185m);

        var partnerPaid = UsageBillingCalculator.Compute(
            Subscription(5000m, 149m, 0.03m, ActivationFeePayer.Partner), usage: 6200m, June);
        partnerPaid.Payer.ShouldBe(ActivationFeePayer.Partner);
        // Partner payer → receivable on 1250.
        partnerPaid.Journal.LineFor(SettlementAccountCode.ArPartner, EntryDirection.Debit)!.Amount.Amount.ShouldBe(185m);
        partnerPaid.Journal.IsBalanced.ShouldBeTrue();
    }

    // ---- Proration (base by calendar days) + overage counted to deactivation -----------------

    [Fact]
    public void Base_Is_Prorated_By_Calendar_Days_Overage_Counted_To_Deactivation()
    {
        var pkg = Subscription(included: 5000m, baseFee: 300m, overFee: 0.10m, ActivationFeePayer.Merchant);
        // Active 15 of 30 days → base 150; usage 6000 (whole units to deactivation) → excess 1000 × 0.10 = 100.
        var r = UsageBillingCalculator.Compute(pkg, usage: 6000m, June, activeDays: 15);
        r.Fee!.BaseInclusive.ShouldBe(150m);
        r.Fee.OverageExcess.ShouldBe(1000m); // usage NOT prorated — counted to deactivation
        r.Fee.OverageInclusive.ShouldBe(100m);
        r.Fee.TotalInclusive.ShouldBe(250m);
    }

    [Fact]
    public void Full_Month_When_Active_Days_Null()
    {
        var pkg = Subscription(included: 0m, baseFee: 300m, overFee: 0m, ActivationFeePayer.Merchant);
        var r = UsageBillingCalculator.Compute(pkg, usage: 0m, June);
        r.ActiveDays.ShouldBe(30);
        r.Fee!.BaseInclusive.ShouldBe(300m);
    }

    // ---- VAT split via template, reconciles ---------------------------------------------------

    [Fact]
    public void Vat_Is_Split_By_The_Template_And_Legs_Reconcile()
    {
        // Fee 115 inclusive @15% → net 100, VAT 15.
        var pkg = Subscription(included: 0m, baseFee: 115m, overFee: 0m, ActivationFeePayer.Merchant);
        var r = UsageBillingCalculator.Compute(pkg, usage: 0m, June);
        r.Journal.LineFor(SettlementAccountCode.FeeRevenue, EntryDirection.Credit)!.Amount.Amount.ShouldBe(100m);
        r.Journal.LineFor(SettlementAccountCode.OutputVat, EntryDirection.Credit)!.Amount.Amount.ShouldBe(15m);
        r.Journal.TotalDebits.Amount.ShouldBe(r.Journal.TotalCredits.Amount);
    }

    // ---- COMPUTE-ONLY: nothing posted, flag OFF -----------------------------------------------

    [Fact]
    public void Compute_Only_Posting_Flag_Is_Off_And_Journal_Nets_Zero()
    {
        new SettlementEngineOptions().PostingEnabled.ShouldBeFalse();

        var r = UsageBillingCalculator.Compute(
            Resale(10000m, 200m, 300m, 0.02m, 0.05m), usage: 12000m, June);

        // A computed (un-posted) journal that nets to zero — no money has moved.
        r.Journal.TrialBalanceNet.ShouldBe(0m);
    }

    [Fact]
    public void Negative_Usage_Is_Rejected()
    {
        Should.Throw<Volo.Abp.BusinessException>(() =>
            UsageBillingCalculator.Compute(Subscription(0m, 1m, 1m, ActivationFeePayer.Merchant), usage: -1m, June));
    }
}
