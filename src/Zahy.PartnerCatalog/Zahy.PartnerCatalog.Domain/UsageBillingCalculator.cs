using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// U5 — one graduated tier's contribution to the overage: <c>Units × Rate</c> within [From, To).
/// Display detail only; the leg's authoritative overage is <see cref="UsageBillingLine.OverageInclusive"/>.
/// </summary>
public sealed record UsageTierLine(
    decimal FromQuantity,
    decimal? ToQuantity,
    decimal Rate,
    decimal Units,
    decimal AmountInclusive);

/// <summary>
/// One computed price leg: prorated base + overage, all VAT-inclusive.
///   total = proratedBase + overage, where overage is either flat (max(0, usage − included) × OverageRate)
///   or the U5 GRADUATED tier sum (Σ units-in-tier × tier rate). <see cref="Tiers"/> is empty for flat.
/// </summary>
public sealed record UsageBillingLine(
    decimal BaseInclusive,
    decimal OverageExcess,
    decimal OverageRate,
    decimal OverageInclusive,
    decimal TotalInclusive,
    IReadOnlyList<UsageTierLine> Tiers);

/// <summary>
/// U3 — the computed usage-billing result for one merchant × package × period. CONFIG/COMPUTE ONLY:
/// the amount is routed to the EXISTING posting template per the package mode (Principal for resale,
/// Fee for subscription) but NOTHING is posted — <c>SettlementEngineOptions.PostingEnabled</c> stays
/// OFF, exactly like the activation-fee / bank-routing compute-only previews. No money moves.
/// </summary>
public sealed class UsageBillingResult
{
    public Guid PackageId { get; }
    public UsagePackageMode Mode { get; }
    public string Currency { get; }
    public decimal Usage { get; }
    public int ActiveDays { get; }
    public int DaysInMonth { get; }

    /// <summary>Resale only — what Zahy pays the partner (buy leg). Null for subscription.</summary>
    public UsageBillingLine? Buy { get; }

    /// <summary>Resale only — what Zahy charges the merchant (sell leg). Null for subscription.</summary>
    public UsageBillingLine? Sell { get; }

    /// <summary>Subscription only — the kept fee. Null for resale.</summary>
    public UsageBillingLine? Fee { get; }

    /// <summary>Subscription only — who pays the fee. Null for resale.</summary>
    public ActivationFeePayer? Payer { get; }

    /// <summary>Inclusive margin = sell − buy (resale only; 0 for subscription).</summary>
    public decimal MarginInclusive { get; }

    /// <summary>
    /// The journal produced by the EXISTING template (Principal or Fee) — COMPUTE ONLY, never posted.
    /// Its derived <c>Margin</c> (net) and <c>NetVat</c> come straight from the template.
    /// </summary>
    public PostingResult Journal { get; }

    internal UsageBillingResult(
        Guid packageId,
        UsagePackageMode mode,
        string currency,
        decimal usage,
        int activeDays,
        int daysInMonth,
        UsageBillingLine? buy,
        UsageBillingLine? sell,
        UsageBillingLine? fee,
        ActivationFeePayer? payer,
        decimal marginInclusive,
        PostingResult journal)
    {
        PackageId = packageId;
        Mode = mode;
        Currency = currency;
        Usage = usage;
        ActiveDays = activeDays;
        DaysInMonth = daysInMonth;
        Buy = buy;
        Sell = sell;
        Fee = fee;
        Payer = payer;
        MarginInclusive = marginInclusive;
        Journal = journal;
    }
}

/// <summary>
/// U3 — USAGE BILLING CALCULATION. Computes the period amount from a U2 <see cref="UsagePackage"/> and the
/// U1 usage quantity, then routes it to the EXISTING posting template per the package mode. COMPUTE ONLY:
///   • RESALE      → buy + sell legs → <see cref="SettlementPostingTemplates.Principal"/> (5100/1300/2100 buy,
///                   1200/4100/2200 sell); margin = sell − buy.
///   • SUBSCRIPTION → fee leg (payer-selectable) → <see cref="SettlementPostingTemplates.Fee"/> (1200/1250→4200+2200).
/// No new posting logic, no new VAT math (VAT is the template's <see cref="VatMath"/>). PRORATION reuses the
/// locked rule: the BASE is pro-rated by ACTUAL CALENDAR DAYS; usage (overage) is whole units already counted
/// to the deactivation moment. Money: 4dp intermediate, 2dp final, AwayFromZero. NOTHING is posted.
/// </summary>
public static class UsageBillingCalculator
{
    public const decimal DefaultVatRate = SettlementVatOptions.DefaultStandardRate;

    /// <param name="usage">U1 usageForPeriod(partner, merchant, period) — whole units to the deactivation moment.</param>
    /// <param name="activeDays">Calendar days the package was active in the period; null = the full month.</param>
    public static UsageBillingResult Compute(
        UsagePackage package,
        decimal usage,
        SettlementPeriod period,
        int? activeDays = null,
        decimal vatRate = DefaultVatRate)
    {
        Check.NotNull(package, nameof(package));
        Check.NotNull(period, nameof(period));
        if (usage < 0m)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackage)
                .WithData("Reason", "NegativeUsage");
        }

        var daysInMonth = DateTime.DaysInMonth(period.Year, period.Month);
        var days = activeDays ?? daysInMonth;
        if (days < 0 || days > daysInMonth)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackage)
                .WithData("Reason", "ActiveDaysOutOfRange")
                .WithData("ActiveDays", days)
                .WithData("DaysInMonth", daysInMonth);
        }

        var currency = package.Currency;

        var tiers = package.Tiers;

        if (package.Mode == UsagePackageMode.Subscription)
        {
            // Subscription fee uses the SELL rate (tiers carry the fee rate; buy is 0).
            var fee = ComputeLine(package.BaseSellAmount, package.IncludedQuantity, package.OverageSellAmount, usage, days, daysInMonth, tiers, t => t.SellRate);
            var journal = SettlementPostingTemplates.Fee(
                Money.Of(fee.TotalInclusive, currency, vatInclusive: true),
                package.Payer,
                vatRate);

            return new UsageBillingResult(
                package.Id, package.Mode, currency, usage, days, daysInMonth,
                buy: null, sell: null, fee: fee, payer: package.Payer,
                marginInclusive: 0m, journal: journal);
        }

        // RESALE — buy and sell legs computed independently (each tier carries a buy/sell rate), routed to Principal.
        var buy = ComputeLine(package.BaseBuyAmount, package.IncludedQuantity, package.OverageBuyAmount, usage, days, daysInMonth, tiers, t => t.BuyRate);
        var sell = ComputeLine(package.BaseSellAmount, package.IncludedQuantity, package.OverageSellAmount, usage, days, daysInMonth, tiers, t => t.SellRate);

        var principal = SettlementPostingTemplates.Principal(
            Money.Of(sell.TotalInclusive, currency, vatInclusive: true),
            Money.Of(buy.TotalInclusive, currency, vatInclusive: true),
            vatRate);

        var marginInclusive = Round2(sell.TotalInclusive - buy.TotalInclusive);

        return new UsageBillingResult(
            package.Id, package.Mode, currency, usage, days, daysInMonth,
            buy: buy, sell: sell, fee: null, payer: null,
            marginInclusive: marginInclusive, journal: principal);
    }

    /// <summary>
    /// amount = proratedBase + overage. Overage is the flat <paramref name="overageRate"/> × excess when the
    /// package has NO tiers (exactly U3), or the U5 GRADUATED tier sum when tiers exist. VAT-inclusive throughout.
    /// </summary>
    private static UsageBillingLine ComputeLine(
        decimal baseAmount,
        decimal includedQuantity,
        decimal overageRate,
        decimal usage,
        int activeDays,
        int daysInMonth,
        IReadOnlyList<UsagePackageTier> tiers,
        Func<UsagePackageTier, decimal> rateOf)
    {
        // Base pro-rated by ACTUAL CALENDAR DAYS (reuse the locked subscription proration rule).
        var proratedBase = activeDays >= daysInMonth
            ? Round2(baseAmount)
            : Round2(baseAmount * activeDays / daysInMonth);

        // Overage = whole units beyond the included allowance, counted to deactivation (usage already is).
        var excess = Math.Max(0m, usage - includedQuantity);

        decimal overageInclusive;
        IReadOnlyList<UsageTierLine> tierLines;
        if (tiers.Count == 0)
        {
            // Back-compat: flat overage, identical to U3.
            overageInclusive = Round2(Round4(excess * overageRate)); // 4dp intermediate → 2dp final
            tierLines = Array.Empty<UsageTierLine>();
        }
        else
        {
            (overageInclusive, tierLines) = ComputeGraduatedOverage(excess, tiers, rateOf);
        }

        var total = Round2(proratedBase + overageInclusive);

        return new UsageBillingLine(proratedBase, excess, overageRate, overageInclusive, total, tierLines);
    }

    /// <summary>
    /// GRADUATED (marginal) overage: each tier's units are charged at THAT tier's rate. Units-in-tier =
    /// max(0, min(excess, To) − From); open-ended top tier (To == null) takes all remaining units. Each
    /// tier accrues at 4dp, the leg total rounds to 2dp — same precision rule as the flat path.
    /// </summary>
    private static (decimal OverageInclusive, IReadOnlyList<UsageTierLine> Lines) ComputeGraduatedOverage(
        decimal excess,
        IReadOnlyList<UsagePackageTier> tiers,
        Func<UsagePackageTier, decimal> rateOf)
    {
        var lines = new List<UsageTierLine>();
        var sum4 = 0m;
        foreach (var tier in tiers.OrderBy(t => t.FromQuantity))
        {
            var upper = tier.ToQuantity ?? excess;
            var units = Math.Max(0m, Math.Min(excess, upper) - tier.FromQuantity);
            if (units <= 0m)
            {
                continue;
            }

            var rate = rateOf(tier);
            var amount4 = Round4(units * rate);
            sum4 += amount4;
            lines.Add(new UsageTierLine(tier.FromQuantity, tier.ToQuantity, rate, units, Round2(amount4)));
        }

        return (Round2(sum4), lines);
    }

    private static decimal Round2(decimal v) => SettlementMoney.Round(v);

    private static decimal Round4(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
}
