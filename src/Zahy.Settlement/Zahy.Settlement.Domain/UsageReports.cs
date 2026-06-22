using System;
using System.Collections.Generic;
using System.Linq;

namespace Zahy.Settlement;

/// <summary>Who is asking — drives row-level scope over usage records (RBAC, data-only).</summary>
public enum UsageScopeKind
{
    /// <summary>Accountant / platform admin — sees ALL usage across every partner + merchant.</summary>
    Platform = 1,

    /// <summary>A partner — sees ONLY the usage of their own merchants (their partnerId).</summary>
    Partner = 2,

    /// <summary>A merchant — sees ONLY their own usage (their merchantId/tenant).</summary>
    Merchant = 3,
}

/// <summary>
/// The viewer for usage row-scope. No money is involved, but usage is still scoped: a partner never
/// sees another partner's usage, and a merchant never sees another merchant's. Build with the factories.
/// </summary>
public sealed record UsageViewer
{
    public UsageScopeKind Kind { get; }

    public Guid? PartnerId { get; }

    public Guid? MerchantId { get; }

    private UsageViewer(UsageScopeKind kind, Guid? partnerId, Guid? merchantId)
    {
        Kind = kind;
        PartnerId = partnerId;
        MerchantId = merchantId;
    }

    /// <summary>Accountant / platform admin — unrestricted.</summary>
    public static UsageViewer Platform() => new(UsageScopeKind.Platform, null, null);

    public static UsageViewer Partner(Guid partnerId) => new(UsageScopeKind.Partner, partnerId, null);

    public static UsageViewer Merchant(Guid merchantId) => new(UsageScopeKind.Merchant, null, merchantId);
}

/// <summary>Per-merchant usage total for one partner + period (sum of that merchant's records).</summary>
public sealed record UsageMerchantTotal(Guid MerchantId, string UnitLabel, decimal Quantity);

/// <summary>A partner's usage rollup for one period: each merchant's total + the partner grand total.</summary>
public sealed record UsagePartnerRollup(
    Guid PartnerId,
    SettlementPeriod Period,
    IReadOnlyList<UsageMerchantTotal> PerMerchant,
    decimal TotalQuantity);

/// <summary>
/// U1 read models over <see cref="UsageRecord"/>s — READ ONLY, pure, no money. Records ACCUMULATE:
/// several incremental rows for the same partner+merchant+period sum together.
/// </summary>
public static class UsageReports
{
    /// <summary>Apply RBAC row-scope: platform sees all; partner sees own merchants; merchant sees own.</summary>
    public static IReadOnlyList<UsageRecord> Scope(IEnumerable<UsageRecord> records, UsageViewer viewer)
    {
        var all = records as IReadOnlyList<UsageRecord> ?? records.ToList();
        return viewer.Kind switch
        {
            UsageScopeKind.Platform => all,
            UsageScopeKind.Partner => all.Where(r => r.PartnerId == viewer.PartnerId).ToList(),
            UsageScopeKind.Merchant => all.Where(r => r.MerchantId == viewer.MerchantId).ToList(),
            _ => Array.Empty<UsageRecord>(),
        };
    }

    /// <summary>
    /// Total units a merchant consumed for a partner in a period — sums ALL matching (incremental)
    /// records. Returns 0 when none match.
    /// </summary>
    public static decimal UsageForPeriod(
        IEnumerable<UsageRecord> records,
        Guid partnerId,
        Guid merchantId,
        SettlementPeriod period)
    {
        return records
            .Where(r => r.PartnerId == partnerId
                && r.MerchantId == merchantId
                && r.PeriodYear == period.Year
                && r.PeriodMonth == period.Month)
            .Sum(r => r.Quantity);
    }

    /// <summary>Per-partner / per-period rollup: each merchant's summed usage + the partner grand total.</summary>
    public static UsagePartnerRollup PartnerPeriodRollup(
        IEnumerable<UsageRecord> records,
        Guid partnerId,
        SettlementPeriod period)
    {
        var inScope = records
            .Where(r => r.PartnerId == partnerId
                && r.PeriodYear == period.Year
                && r.PeriodMonth == period.Month)
            .ToList();

        var perMerchant = inScope
            .GroupBy(r => r.MerchantId)
            .Select(g => new UsageMerchantTotal(
                g.Key,
                g.Select(r => r.UnitLabel).FirstOrDefault() ?? SettlementUsageConsts.DefaultUnitLabel,
                g.Sum(r => r.Quantity)))
            .OrderBy(t => t.MerchantId)
            .ToList();

        var total = perMerchant.Sum(t => t.Quantity);
        return new UsagePartnerRollup(partnerId, period, perMerchant, total);
    }
}
