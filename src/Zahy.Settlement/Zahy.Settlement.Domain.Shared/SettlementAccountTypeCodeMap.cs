using System.Collections.Generic;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// THE single source of truth binding the legacy <see cref="SettlementAccountType"/> enum to the
/// coded <see cref="SettlementChartOfAccounts"/>. Every enum leg resolves to exactly one chart code,
/// so the two representations cannot drift. New posting templates post against codes directly; this
/// map keeps anything still expressed as the enum aligned to the same chart.
/// </summary>
public static class SettlementAccountTypeCodeMap
{
    private static readonly IReadOnlyDictionary<SettlementAccountType, string> Map =
        new Dictionary<SettlementAccountType, string>
        {
            [SettlementAccountType.AggregatorClearing] = SettlementAccountCode.BankCashClearing,   // 1100
            [SettlementAccountType.MerchantPayable] = SettlementAccountCode.ArMerchant,             // 1200 (merchant control account)
            [SettlementAccountType.VatInput] = SettlementAccountCode.InputVat,                      // 1300
            [SettlementAccountType.PartnerPayable] = SettlementAccountCode.ApPartner,               // 2100
            [SettlementAccountType.VatOutput] = SettlementAccountCode.OutputVat,                    // 2200
            [SettlementAccountType.ShippingMarginRevenue] = SettlementAccountCode.ResaleRevenue,    // 4100
            [SettlementAccountType.PlatformCommissionRevenue] = SettlementAccountCode.FeeRevenue,   // 4200
            [SettlementAccountType.DeliveryCost] = SettlementAccountCode.PartnerCogs               // 5100
        };

    public static IReadOnlyDictionary<SettlementAccountType, string> All => Map;

    public static string CodeOf(SettlementAccountType account) =>
        Map.TryGetValue(account, out var code)
            ? code
            : throw new AbpException($"No chart code mapped for settlement account '{account}'.");
}
