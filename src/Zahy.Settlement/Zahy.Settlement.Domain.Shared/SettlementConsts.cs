using System;

namespace Zahy.Settlement;

public static class SettlementConsts
{
    public const string DefaultCurrency = "SAR";

    /// <summary>All settlement money is posted at 2 decimal places (round-per-line policy, CTO-approved Phase 0).</summary>
    public const int MoneyScale = 2;
}

/// <summary>
/// Centralized money rounding for the Settlement context. Round-per-line, 2dp, away-from-zero —
/// consistent with the existing CommissionMoney / FinanceMoney helpers.
/// </summary>
public static class SettlementMoney
{
    public static decimal Round(decimal value) =>
        Math.Round(value, SettlementConsts.MoneyScale, MidpointRounding.AwayFromZero);
}

public static class SettlementErrorCodes
{
    public const string Namespace = "Zahy.Settlement";

    public const string InvalidCurrency = Namespace + ":001";
    public const string CurrencyMismatch = Namespace + ":002";
    public const string VatInclusiveMismatch = Namespace + ":003";
    public const string NonPositiveAmount = Namespace + ":004";
    public const string UnbalancedJournal = Namespace + ":005";

    /// <summary>A journal with fewer than two legs cannot express a double entry.</summary>
    public const string DegenerateJournal = Namespace + ":006";
}
