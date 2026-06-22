namespace Zahy.PartnerCatalog;

/// <summary>
/// U2 — how a usage-based offering is priced. Chosen per package by the partner.
/// RESALE: base + overage carry a BUY/SELL pair (partner buy, Zahy sell) — Zahy pays the partner and
/// keeps the margin (mirrors how Principal catalog items carry buy/sell).
/// SUBSCRIPTION: base + overage are Zahy's FEE, kept entirely (no partner payout); payer selectable.
/// CONFIG ONLY — no billing math lives on this enum; U3 does the calc.
/// </summary>
public enum UsagePackageMode
{
    Resale = 1,
    Subscription = 2,
}

/// <summary>Authoring lifecycle for a usage package (partner self-publishes; admin manages-all).</summary>
public enum UsagePackageStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3,
}

/// <summary>
/// U4 — lifecycle of a MERCHANT's selection of a published usage package (instant self-service activation,
/// no approval). The active window (ActivatedAt..EndedAt) is what U3 prorates the base over; usage is
/// counted to the deactivation moment. SELECTION/LINK ONLY — holding this row computes no billing.
/// </summary>
public enum UsagePackageSelectionStatus
{
    Active = 1,
    Ended = 2,
}

/// <summary>Field limits for the U2 usage package (config + authoring only).</summary>
public static class UsagePackageConsts
{
    public const string DefaultCurrency = Settlement.SettlementConsts.DefaultCurrency;

    public const int MaxNameLength = 256;

    /// <summary>The consumed unit, e.g. "messages" — must match the U1 usage unit label.</summary>
    public const int MaxUnitLabelLength = 32;

    /// <summary>Guardrail on the optional U5 volume-tier ladder (keeps the JSON column bounded).</summary>
    public const int MaxVolumeTiers = 12;
}

/// <summary>
/// U5 — an OPTIONAL volume tier on a usage package's OVERAGE (units beyond IncludedQuantity). Tiers form a
/// GRADUATED (marginal) ladder measured in OVERAGE units from 0: each bracket's units are charged at that
/// bracket's rate (e.g. first 10,000 overage units @0.05, next units @0.04). When a package has NO tiers,
/// billing is EXACTLY the flat U3 overage rate — fully back-compatible. Like the flat overage, a tier
/// carries a BUY/SELL rate pair (resale: partner buy + Zahy sell; subscription: sell is the fee, buy = 0).
/// CONFIG ONLY — no billing math lives here; U3 does the graduated calc.
/// </summary>
public sealed class UsagePackageTier
{
    /// <summary>Inclusive lower bound, in OVERAGE units from 0 (first tier must start at 0).</summary>
    public decimal FromQuantity { get; set; }

    /// <summary>Exclusive upper bound, in OVERAGE units; null = open-ended top tier.</summary>
    public decimal? ToQuantity { get; set; }

    /// <summary>Per-unit BUY rate (what Zahy pays the partner). Forced to 0 for subscription. VAT-inclusive.</summary>
    public decimal BuyRate { get; set; }

    /// <summary>Per-unit SELL rate (what Zahy charges / the kept fee). VAT-inclusive.</summary>
    public decimal SellRate { get; set; }
}
