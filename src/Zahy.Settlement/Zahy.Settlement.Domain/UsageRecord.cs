using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Zahy.Settlement;

/// <summary>
/// U1 — a metered-usage record: how many UNITS a merchant consumed for a given partner in a given
/// period (e.g. Pizza House used 6,200 messages via WhatsApp Co in 2026-06). Keyed to
/// partner + merchant + period. APPEND / ACCUMULATE model — usage can be recorded incrementally and
/// summed per period (see <see cref="UsageReports.UsageForPeriod"/>), so several rows for the same
/// partner+merchant+period are expected and add up.
///
/// PURE DATA — holding this row computes NO fee and posts NO journal. Usage-based pricing/posting is
/// the separately-gated U3 phase. The trial balance is never affected by usage records.
/// </summary>
public class UsageRecord : FullAuditedAggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    /// <summary>The merchant (tenant) that consumed the units.</summary>
    public Guid MerchantId { get; private set; }

    public int PeriodYear { get; private set; }

    public int PeriodMonth { get; private set; }

    /// <summary>The consumed unit, e.g. "messages". Free-form label — no pricing is attached here.</summary>
    public string UnitLabel { get; private set; } = SettlementUsageConsts.DefaultUnitLabel;

    /// <summary>Quantity consumed in this (incremental) record. Non-negative; rows accumulate per period.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Provenance: "seed" / "manual" / "partner-feed" (the gated auto-feed seam).</summary>
    public string Source { get; private set; } = SettlementUsageConsts.DefaultSource;

    /// <summary>The period as a value (derived from the scalar Year/Month columns, never stored).</summary>
    public SettlementPeriod Period => SettlementPeriod.Of(PeriodYear, PeriodMonth);

    protected UsageRecord()
    {
    }

    public UsageRecord(
        Guid id,
        Guid partnerId,
        Guid merchantId,
        SettlementPeriod period,
        string unitLabel,
        decimal quantity,
        string? source = null)
        : base(id)
    {
        if (partnerId == Guid.Empty)
        {
            throw new AbpException("Usage record requires a partnerId.");
        }

        if (merchantId == Guid.Empty)
        {
            throw new AbpException("Usage record requires a merchantId.");
        }

        Check.NotNull(period, nameof(period));
        if (quantity < 0m)
        {
            throw new AbpException($"Usage quantity cannot be negative ('{quantity}').");
        }

        PartnerId = partnerId;
        MerchantId = merchantId;
        PeriodYear = period.Year;
        PeriodMonth = period.Month;
        UnitLabel = NormalizeUnit(unitLabel);
        Quantity = quantity;
        Source = NormalizeSource(source);
    }

    private static string NormalizeUnit(string unitLabel)
    {
        if (string.IsNullOrWhiteSpace(unitLabel))
        {
            return SettlementUsageConsts.DefaultUnitLabel;
        }

        return Check.Length(unitLabel.Trim(), nameof(unitLabel), SettlementUsageConsts.MaxUnitLabelLength);
    }

    private static string NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return SettlementUsageConsts.DefaultSource;
        }

        return Check.Length(source.Trim(), nameof(source), SettlementUsageConsts.MaxSourceLength);
    }
}
