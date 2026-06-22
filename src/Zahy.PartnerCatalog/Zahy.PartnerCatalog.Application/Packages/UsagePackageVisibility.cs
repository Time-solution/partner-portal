using System.Linq;
using Zahy.PartnerCatalog.Read;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog.Packages;

/// <summary>
/// U2 scoped pricing visibility — the SINGLE source for which package money fields each audience sees.
/// Same standard as catalog pricing: MARGIN is admin-only and is STRUCTURALLY ABSENT (null) from
/// partner/merchant DTOs, not merely hidden. For RESALE the partner sees BUY, the merchant sees SELL;
/// for SUBSCRIPTION there is only the fee (sell) and no buy/margin. PURE — no money math beyond the
/// trivial sell−buy margin DISPLAY value (admin only); computes no billing and posts nothing.
/// </summary>
public static class UsagePackageVisibility
{
    public static UsagePackageDto ToDto(UsagePackage package, UsagePackageAudience audience)
    {
        var dto = new UsagePackageDto
        {
            Id = package.Id,
            PartnerId = package.PartnerId,
            Name = package.Name,
            UnitLabel = package.UnitLabel,
            Mode = package.Mode,
            Currency = package.Currency,
            IncludedQuantity = package.IncludedQuantity,
            Status = package.Status,
            Audience = audience,
            Payer = package.Mode == UsagePackageMode.Subscription ? package.Payer : null,
            Tiers = package.Tiers.Select(t => ScopeTier(t, package.Mode, package.Currency, audience)).ToList(),
        };

        var buyBase = Money(package.BaseBuyAmount, package.Currency);
        var buyOver = Money(package.OverageBuyAmount, package.Currency);
        var sellBase = Money(package.BaseSellAmount, package.Currency);
        var sellOver = Money(package.OverageSellAmount, package.Currency);

        if (package.Mode == UsagePackageMode.Subscription)
        {
            // Fee only — no buy/margin concept for any audience.
            dto.BaseSell = sellBase;
            dto.OverageSell = sellOver;
            return dto;
        }

        // RESALE — scoped buy/sell/margin.
        switch (audience)
        {
            case UsagePackageAudience.Admin:
                dto.BaseBuy = buyBase;
                dto.OverageBuy = buyOver;
                dto.BaseSell = sellBase;
                dto.OverageSell = sellOver;
                dto.BaseMargin = Money(package.BaseSellAmount - package.BaseBuyAmount, package.Currency);
                dto.OverageMargin = Money(package.OverageSellAmount - package.OverageBuyAmount, package.Currency);
                break;

            case UsagePackageAudience.Partner:
                // BUY side only — sell + margin are absent (null), never hidden-but-present.
                dto.BaseBuy = buyBase;
                dto.OverageBuy = buyOver;
                break;

            case UsagePackageAudience.Merchant:
                // SELL side only — buy + margin absent.
                dto.BaseSell = sellBase;
                dto.OverageSell = sellOver;
                break;
        }

        return dto;
    }

    /// <summary>Scope one overage tier the same way as the flat overage: partner→buy, merchant→sell, margin admin-only.</summary>
    private static UsagePackageTierDto ScopeTier(
        UsagePackageTier tier,
        UsagePackageMode mode,
        string currency,
        UsagePackageAudience audience)
    {
        var dto = new UsagePackageTierDto { FromQuantity = tier.FromQuantity, ToQuantity = tier.ToQuantity };

        if (mode == UsagePackageMode.Subscription)
        {
            // Fee only — the sell rate is the fee; no buy/margin.
            dto.SellRate = Money(tier.SellRate, currency);
            return dto;
        }

        switch (audience)
        {
            case UsagePackageAudience.Admin:
                dto.BuyRate = Money(tier.BuyRate, currency);
                dto.SellRate = Money(tier.SellRate, currency);
                dto.MarginRate = Money(tier.SellRate - tier.BuyRate, currency);
                break;
            case UsagePackageAudience.Partner:
                dto.BuyRate = Money(tier.BuyRate, currency);
                break;
            case UsagePackageAudience.Merchant:
                dto.SellRate = Money(tier.SellRate, currency);
                break;
        }

        return dto;
    }

    private static MoneyDto Money(decimal amount, string currency) =>
        new() { Amount = amount, Currency = currency, VatInclusive = true };
}
