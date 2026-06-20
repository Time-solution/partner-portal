using System.Collections.Generic;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// Normal balance (debit-normal vs credit-normal) per settlement account. This does not enforce
/// posting direction (a credit-normal account can be debited); it documents the chart so the
/// accountant can read each book correctly.
/// </summary>
public static class SettlementAccountChart
{
    private static readonly IReadOnlyDictionary<SettlementAccountType, EntryDirection> NormalBalances =
        new Dictionary<SettlementAccountType, EntryDirection>
        {
            [SettlementAccountType.AggregatorClearing] = EntryDirection.Debit,
            [SettlementAccountType.MerchantPayable] = EntryDirection.Credit,
            [SettlementAccountType.PartnerPayable] = EntryDirection.Credit,
            [SettlementAccountType.DeliveryCost] = EntryDirection.Debit,
            [SettlementAccountType.PlatformCommissionRevenue] = EntryDirection.Credit,
            [SettlementAccountType.ShippingMarginRevenue] = EntryDirection.Credit,
            [SettlementAccountType.VatOutput] = EntryDirection.Credit,
            [SettlementAccountType.VatInput] = EntryDirection.Debit
        };

    public static IReadOnlyDictionary<SettlementAccountType, EntryDirection> All => NormalBalances;

    public static EntryDirection NormalBalanceOf(SettlementAccountType account) =>
        NormalBalances.TryGetValue(account, out var direction)
            ? direction
            : throw new AbpException($"No normal balance defined for settlement account '{account}'.");
}
