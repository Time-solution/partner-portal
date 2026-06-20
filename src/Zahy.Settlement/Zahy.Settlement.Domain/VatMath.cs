using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// VAT-inclusive back-out arithmetic, round-per-line (CTO-approved, DESIGN.md §9.1):
/// for an inclusive price, net = round(price / (1 + rate), 2) and vat = price − net.
/// The back-out (price − net) — rather than net × rate — keeps inclusive == net + vat exactly at 2dp.
/// </summary>
public static class VatMath
{
    public static void EnsureValidRate(decimal rate)
    {
        if (rate < 0m || rate >= 1m)
        {
            throw new BusinessException(SettlementVatErrorCodes.InvalidVatRate).WithData("Rate", rate);
        }
    }

    /// <summary>Net (VAT-exclusive) amount of a VAT-inclusive price, rounded per line.</summary>
    public static decimal NetOfInclusive(decimal inclusiveAmount, decimal rate)
    {
        EnsureValidRate(rate);
        return SettlementMoney.Round(inclusiveAmount / (1m + rate));
    }

    /// <summary>VAT portion of a VAT-inclusive price (back-out): inclusive − net.</summary>
    public static decimal VatOfInclusive(decimal inclusiveAmount, decimal rate) =>
        SettlementMoney.Round(inclusiveAmount - NetOfInclusive(inclusiveAmount, rate));
}
