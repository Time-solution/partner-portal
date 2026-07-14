namespace Zahy.Settlement;

/// <summary>
/// THE calendar-day proration rule (locked subscription rule, CTO-approved): a monthly VAT-INCLUSIVE
/// amount is pro-rated by ACTUAL CALENDAR DAYS active in the period — full amount when active the whole
/// month (or more), otherwise amount × activeDays ÷ daysInMonth — rounded per <see cref="SettlementMoney.Round"/>
/// (2dp, away-from-zero, round-per-line). Single source: both the usage billing calculator (PartnerCatalog)
/// and the invoice report (Settlement) delegate here; do not re-implement this rule elsewhere.
/// Pure math — range validation of <paramref name="activeDays"/> stays at the call sites, which own
/// their context-specific error codes.
/// </summary>
public static class SettlementProration
{
    public static decimal ProrateByCalendarDays(decimal inclusiveAmount, int activeDays, int daysInMonth)
        => activeDays >= daysInMonth
            ? SettlementMoney.Round(inclusiveAmount)
            : SettlementMoney.Round(inclusiveAmount * activeDays / daysInMonth);
}
