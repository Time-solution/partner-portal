using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

public class SettlementAccountCodeMapTests
{
    [Fact]
    public void Account_Code_Constants_Match_The_Seeded_Chart_Exactly()
    {
        var chartCodes = SettlementChartOfAccounts.All.Select(a => a.Code).OrderBy(c => c);
        var codeConsts = SettlementAccountCode.All.OrderBy(c => c);

        codeConsts.ShouldBe(chartCodes);
    }

    [Fact]
    public void Every_Legacy_Enum_Value_Maps_To_A_Real_Chart_Code()
    {
        foreach (var account in Enum.GetValues<SettlementAccountType>())
        {
            var code = SettlementAccountTypeCodeMap.CodeOf(account);
            SettlementAccountCode.IsDefined(code).ShouldBeTrue();
            SettlementChartOfAccounts.All.ShouldContain(a => a.Code == code);
        }
    }

    [Fact]
    public void Enum_To_Code_Mapping_Is_Injective()
    {
        var codes = SettlementAccountTypeCodeMap.All.Values.ToList();
        codes.Distinct().Count().ShouldBe(codes.Count);
    }

    [Fact]
    public void Posting_Templates_Only_Reference_Defined_Codes()
    {
        var result = SettlementPostingTemplates.Principal(
            Money.Of(100m, "SAR", vatInclusive: true),
            Money.Of(70m, "SAR", vatInclusive: true),
            0.15m);

        foreach (var line in result.Lines)
        {
            SettlementAccountCode.IsDefined(line.AccountCode).ShouldBeTrue();
        }
    }
}
