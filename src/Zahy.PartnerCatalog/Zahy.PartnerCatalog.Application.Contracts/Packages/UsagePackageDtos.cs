using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Zahy.PartnerCatalog.Read;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog.Packages;

/// <summary>Who the package view is being rendered for — drives scoped pricing visibility.</summary>
public enum UsagePackageAudience
{
    /// <summary>Accountant / platform admin — sees buy, sell AND margin.</summary>
    Admin = 1,

    /// <summary>The owning partner — RESALE: BUY side only (sell + margin absent). Subscription: fee.</summary>
    Partner = 2,

    /// <summary>A merchant (U4) — RESALE: SELL side only (buy + margin absent). Subscription: fee.</summary>
    Merchant = 3,
}

/// <summary>
/// U2 read DTO for a usage package. Pricing fields are AUDIENCE-SCOPED and may be null when not visible
/// (structural absence, the same standard as catalog pricing — margin is admin-only). CONFIG ONLY: no
/// billing is computed, no journal posted.
/// </summary>
public class UsagePackageDto : EntityDto<Guid>
{
    public Guid PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UnitLabel { get; set; } = string.Empty;
    public UsagePackageMode Mode { get; set; }
    public string Currency { get; set; } = UsagePackageConsts.DefaultCurrency;
    public decimal IncludedQuantity { get; set; }
    public UsagePackageStatus Status { get; set; }
    public UsagePackageAudience Audience { get; set; }

    /// <summary>Subscription only — who pays the fee (Merchant/Partner). Null for resale.</summary>
    public ActivationFeePayer? Payer { get; set; }

    /// <summary>What Zahy PAYS the partner (resale buy side). Visible to partner + admin; null otherwise.</summary>
    public MoneyDto? BaseBuy { get; set; }
    public MoneyDto? OverageBuy { get; set; }

    /// <summary>What Zahy SELLS / the kept fee. Admin + merchant; for subscription this is the fee. </summary>
    public MoneyDto? BaseSell { get; set; }
    public MoneyDto? OverageSell { get; set; }

    /// <summary>Margin = sell − buy. ADMIN-ONLY — null/absent for partner and merchant renders.</summary>
    public MoneyDto? BaseMargin { get; set; }
    public MoneyDto? OverageMargin { get; set; }

    /// <summary>
    /// U5 — OPTIONAL graduated overage tiers (empty = flat overage). Rates are AUDIENCE-SCOPED exactly like
    /// the flat overage: partner sees buy, merchant sees sell, margin admin-only; subscription = sell (fee).
    /// </summary>
    public List<UsagePackageTierDto> Tiers { get; set; } = new();
}

/// <summary>U5 — one audience-scoped overage tier. Rate fields are null when not visible to the audience.</summary>
public class UsagePackageTierDto
{
    public decimal FromQuantity { get; set; }
    public decimal? ToQuantity { get; set; }
    public MoneyDto? BuyRate { get; set; }
    public MoneyDto? SellRate { get; set; }
    public MoneyDto? MarginRate { get; set; }
}

/// <summary>U5 — author input for one overage tier (from/to in overage units, with a buy/sell rate pair).</summary>
public class UsagePackageTierInput
{
    public decimal FromQuantity { get; set; }
    public decimal? ToQuantity { get; set; }
    public decimal BuyRate { get; set; }
    public decimal SellRate { get; set; }
}

public class CreateUsagePackageInput
{
    public Guid PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UnitLabel { get; set; } = "messages";
    public UsagePackageMode Mode { get; set; }
    public string Currency { get; set; } = UsagePackageConsts.DefaultCurrency;
    public decimal IncludedQuantity { get; set; }
    public decimal BaseBuyAmount { get; set; }
    public decimal BaseSellAmount { get; set; }
    public decimal OverageBuyAmount { get; set; }
    public decimal OverageSellAmount { get; set; }
    public ActivationFeePayer Payer { get; set; } = ActivationFeePayer.Merchant;

    /// <summary>U5 — optional graduated overage tiers (empty = flat overage).</summary>
    public List<UsagePackageTierInput> Tiers { get; set; } = new();
}

public class UpdateUsagePackageInput
{
    public string Name { get; set; } = string.Empty;
    public string UnitLabel { get; set; } = "messages";
    public decimal IncludedQuantity { get; set; }
    public decimal BaseBuyAmount { get; set; }
    public decimal BaseSellAmount { get; set; }
    public decimal OverageBuyAmount { get; set; }
    public decimal OverageSellAmount { get; set; }
    public ActivationFeePayer Payer { get; set; } = ActivationFeePayer.Merchant;

    /// <summary>U5 — optional graduated overage tiers (empty = flat overage).</summary>
    public List<UsagePackageTierInput> Tiers { get; set; } = new();
}

public class UsagePackagesQuery
{
    public Guid? PartnerId { get; set; }
}
