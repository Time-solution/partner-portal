using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// A reporting/settlement period (calendar month). Used to group orders, statements and the
/// trial balance / VAT control. Value-equal so it can be used as a grouping key.
/// </summary>
public sealed record SettlementPeriod
{
    public int Year { get; }

    public int Month { get; }

    private SettlementPeriod(int year, int month)
    {
        Year = year;
        Month = month;
    }

    public static SettlementPeriod Of(int year, int month)
    {
        if (year < 1)
        {
            throw new AbpException($"Invalid settlement period year '{year}'.");
        }

        if (month < 1 || month > 12)
        {
            throw new AbpException($"Invalid settlement period month '{month}'.");
        }

        return new SettlementPeriod(year, month);
    }

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
