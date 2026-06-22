using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// U2 — a usage PACKAGE: how a partner's usage-based offering is priced. A partner may have MULTIPLE
/// optional packages. CONFIG ONLY — holding this row computes NO billing and posts NO journal; the
/// usage→charge calculation is the separately-gated U3 phase.
///
/// Prices are stored as a BUY/SELL pair (both VAT-inclusive), mirroring how Principal catalog items
/// carry buy vs sell:
///   • RESALE      → BaseBuy/OverageBuy = what Zahy PAYS the partner; BaseSell/OverageSell = what Zahy
///                   SELLS to the merchant. Margin = sell − buy (admin-only display).
///   • SUBSCRIPTION→ the amount is Zahy's FEE kept entirely (no partner payout): Buy = 0, Sell = the fee.
///                   <see cref="Payer"/> selects who pays (Merchant/Partner), like ActivationFeeLine.
///   • Pure per-use→ BasePrice 0 + IncludedQuantity 0 (rate × usage).
/// </summary>
public class UsagePackage : FullAuditedAggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The consumed unit (must match the U1 usage unit label, e.g. "messages").</summary>
    public string UnitLabel { get; private set; } = "units";

    public UsagePackageMode Mode { get; private set; }

    public string Currency { get; private set; } = UsagePackageConsts.DefaultCurrency;

    /// <summary>Units included in the recurring base before overage applies.</summary>
    public decimal IncludedQuantity { get; private set; }

    /// <summary>Recurring base — what Zahy PAYS the partner (resale); 0 for subscription. VAT-inclusive.</summary>
    public decimal BaseBuyAmount { get; private set; }

    /// <summary>Recurring base — what Zahy SELLS / the kept fee. VAT-inclusive.</summary>
    public decimal BaseSellAmount { get; private set; }

    /// <summary>Per-unit overage beyond included — partner buy side; 0 for subscription. VAT-inclusive.</summary>
    public decimal OverageBuyAmount { get; private set; }

    /// <summary>Per-unit overage beyond included — Zahy sell / kept fee. VAT-inclusive.</summary>
    public decimal OverageSellAmount { get; private set; }

    /// <summary>Who pays the subscription fee (subscription mode only). Ignored for resale.</summary>
    public ActivationFeePayer Payer { get; private set; } = ActivationFeePayer.Merchant;

    public UsagePackageStatus Status { get; private set; } = UsagePackageStatus.Draft;

    /// <summary>
    /// U5 — OPTIONAL graduated volume-tier ladder for the overage, persisted as JSON (provider-agnostic;
    /// null/empty = flat U3 overage, fully back-compatible). Read via <see cref="Tiers"/>; set via
    /// <see cref="SetTiers"/>. CONFIG ONLY.
    /// </summary>
    public string? TiersJson { get; private set; }

    private static readonly JsonSerializerOptions TierJson = new();

    /// <summary>The (deserialized, ordered) volume tiers — empty when the package is flat-rated.</summary>
    public IReadOnlyList<UsagePackageTier> Tiers => DeserializeTiers(TiersJson);

    protected UsagePackage()
    {
    }

    public UsagePackage(
        Guid id,
        Guid partnerId,
        string name,
        string unitLabel,
        UsagePackageMode mode,
        string currency,
        decimal includedQuantity,
        decimal baseBuyAmount,
        decimal baseSellAmount,
        decimal overageBuyAmount,
        decimal overageSellAmount,
        ActivationFeePayer payer)
        : base(id)
    {
        if (partnerId == Guid.Empty)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackage)
                .WithData("Reason", "PartnerIdRequired");
        }

        PartnerId = partnerId;
        Mode = mode;
        Currency = NormalizeCurrency(currency);
        SetDetails(name, unitLabel, includedQuantity, baseBuyAmount, baseSellAmount, overageBuyAmount, overageSellAmount, payer);
        Status = UsagePackageStatus.Draft;
    }

    /// <summary>Edit the (draft or published) package config. NEVER computes billing.</summary>
    public void SetDetails(
        string name,
        string unitLabel,
        decimal includedQuantity,
        decimal baseBuyAmount,
        decimal baseSellAmount,
        decimal overageBuyAmount,
        decimal overageSellAmount,
        ActivationFeePayer payer)
    {
        Name = NormalizeName(name);
        UnitLabel = NormalizeUnit(unitLabel);
        EnsureNonNegative(includedQuantity, baseBuyAmount, baseSellAmount, overageBuyAmount, overageSellAmount);

        IncludedQuantity = includedQuantity;

        if (Mode == UsagePackageMode.Subscription)
        {
            // Subscription fee is kept entirely — there is no partner payout (buy side is always 0).
            BaseBuyAmount = 0m;
            OverageBuyAmount = 0m;
            BaseSellAmount = baseSellAmount;
            OverageSellAmount = overageSellAmount;
            Payer = payer;
        }
        else
        {
            BaseBuyAmount = baseBuyAmount;
            BaseSellAmount = baseSellAmount;
            OverageBuyAmount = overageBuyAmount;
            OverageSellAmount = overageSellAmount;
            // Payer is a subscription concept; default for resale.
            Payer = ActivationFeePayer.Merchant;
        }
    }

    public void Publish()
    {
        if (Status == UsagePackageStatus.Archived)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackageStatusTransition)
                .WithData("From", Status.ToString())
                .WithData("To", UsagePackageStatus.Published.ToString());
        }

        Status = UsagePackageStatus.Published;
    }

    public void Archive()
    {
        Status = UsagePackageStatus.Archived;
    }

    /// <summary>
    /// U5 — set the OPTIONAL overage tier ladder (null/empty clears it back to flat U3 behavior). Tiers are
    /// ordered, must start at 0, be CONTIGUOUS (each <c>From</c> = previous <c>To</c>), and only the last
    /// may be open-ended. Subscription forces each tier's buy rate to 0 (the sell rate is the fee). CONFIG
    /// ONLY — sets data; computes no billing.
    /// </summary>
    public void SetTiers(IEnumerable<UsagePackageTier>? tiers)
    {
        var list = (tiers ?? Enumerable.Empty<UsagePackageTier>())
            .Select(t => new UsagePackageTier
            {
                FromQuantity = t.FromQuantity,
                ToQuantity = t.ToQuantity,
                BuyRate = Mode == UsagePackageMode.Subscription ? 0m : t.BuyRate,
                SellRate = t.SellRate,
            })
            .OrderBy(t => t.FromQuantity)
            .ToList();

        ValidateTiers(list);

        TiersJson = list.Count == 0 ? null : JsonSerializer.Serialize(list, TierJson);
    }

    private static void ValidateTiers(IReadOnlyList<UsagePackageTier> tiers)
    {
        if (tiers.Count == 0)
        {
            return;
        }

        if (tiers.Count > UsagePackageConsts.MaxVolumeTiers)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackageTier)
                .WithData("Reason", "TooManyTiers");
        }

        for (var i = 0; i < tiers.Count; i++)
        {
            var t = tiers[i];
            if (t.FromQuantity < 0m || t.BuyRate < 0m || t.SellRate < 0m)
            {
                throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackageTier)
                    .WithData("Reason", "NegativeValue");
            }

            if (t.ToQuantity != null && t.ToQuantity <= t.FromQuantity)
            {
                throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackageTier)
                    .WithData("Reason", "ToNotAboveFrom");
            }

            // The ladder is measured in OVERAGE units from 0 and must be contiguous (no gaps/overlaps).
            var expectedFrom = i == 0 ? 0m : tiers[i - 1].ToQuantity ?? -1m;
            if (i == 0 && t.FromQuantity != 0m)
            {
                throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackageTier)
                    .WithData("Reason", "FirstTierMustStartAtZero");
            }

            if (i > 0)
            {
                if (tiers[i - 1].ToQuantity == null)
                {
                    throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackageTier)
                        .WithData("Reason", "OpenEndedTierMustBeLast");
                }

                if (t.FromQuantity != expectedFrom)
                {
                    throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackageTier)
                        .WithData("Reason", "TiersMustBeContiguous");
                }
            }
        }
    }

    private static IReadOnlyList<UsagePackageTier> DeserializeTiers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<UsagePackageTier>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<UsagePackageTier>>(json, TierJson)
                ?? (IReadOnlyList<UsagePackageTier>)Array.Empty<UsagePackageTier>();
        }
        catch (JsonException)
        {
            return Array.Empty<UsagePackageTier>();
        }
    }

    private static void EnsureNonNegative(params decimal[] values)
    {
        foreach (var v in values)
        {
            if (v < 0m)
            {
                throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackage)
                    .WithData("Reason", "NegativeAmount");
            }
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackage)
                .WithData("Reason", "NameRequired");
        }

        return Check.Length(name.Trim(), nameof(name), UsagePackageConsts.MaxNameLength);
    }

    private static string NormalizeUnit(string unitLabel)
    {
        if (string.IsNullOrWhiteSpace(unitLabel))
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackage)
                .WithData("Reason", "UnitLabelRequired");
        }

        return Check.Length(unitLabel.Trim(), nameof(unitLabel), UsagePackageConsts.MaxUnitLabelLength);
    }

    private static string NormalizeCurrency(string currency) =>
        string.IsNullOrWhiteSpace(currency) ? UsagePackageConsts.DefaultCurrency : currency.Trim();
}
