using System;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

public class SettlementAccountChartTests
{
    [Theory]
    [InlineData(SettlementAccountType.AggregatorClearing, EntryDirection.Debit)]
    [InlineData(SettlementAccountType.MerchantPayable, EntryDirection.Credit)]
    [InlineData(SettlementAccountType.PartnerPayable, EntryDirection.Credit)]
    [InlineData(SettlementAccountType.DeliveryCost, EntryDirection.Debit)]
    [InlineData(SettlementAccountType.PlatformCommissionRevenue, EntryDirection.Credit)]
    [InlineData(SettlementAccountType.ShippingMarginRevenue, EntryDirection.Credit)]
    [InlineData(SettlementAccountType.VatOutput, EntryDirection.Credit)]
    [InlineData(SettlementAccountType.VatInput, EntryDirection.Debit)]
    public void Normal_Balances_Are_Defined(SettlementAccountType account, EntryDirection expected)
    {
        SettlementAccountChart.NormalBalanceOf(account).ShouldBe(expected);
    }

    [Fact]
    public void Every_Account_Type_Has_A_Normal_Balance()
    {
        foreach (var account in Enum.GetValues<SettlementAccountType>())
        {
            SettlementAccountChart.All.ShouldContainKey(account);
        }
    }
}
