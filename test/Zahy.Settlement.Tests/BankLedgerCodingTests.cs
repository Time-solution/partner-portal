using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Track B — pure coding rules for per-bank ledger sub-accounts (110x under the 1100 parent). The
/// canonical chart is never touched; these are dynamic registry codes validated only for posting.
/// </summary>
public class BankLedgerCodingTests
{
    [Fact]
    public void Parent_Is_1100()
    {
        BankLedgerCoding.ParentCode.ShouldBe(SettlementAccountCode.BankCashClearing);
        BankLedgerCoding.ParentCode.ShouldBe("1100");
    }

    [Fact]
    public void First_Bank_Gets_1101_Then_1102()
    {
        var first = BankLedgerCoding.NextCode(Array.Empty<string>());
        first.ShouldBe("1101");

        var second = BankLedgerCoding.NextCode(new[] { "1101" });
        second.ShouldBe("1102");

        var third = BankLedgerCoding.NextCode(new[] { "1101", "1102" });
        third.ShouldBe("1103");
    }

    [Fact]
    public void NextCode_Reuses_The_Lowest_Free_Gap()
    {
        // 1101 freed (deactivated/removed) → the next add reuses it before 1103.
        BankLedgerCoding.NextCode(new[] { "1102" }).ShouldBe("1101");
        BankLedgerCoding.NextCode(new[] { "1101", "1103" }).ShouldBe("1102");
    }

    [Fact]
    public void NextCode_Throws_When_The_Reserved_Range_Is_Exhausted()
    {
        var allUsed = Enumerable.Range(BankLedgerCoding.FirstSubCode, BankLedgerCoding.LastSubCode - BankLedgerCoding.FirstSubCode + 1)
            .Select(n => n.ToString())
            .ToArray();

        Should.Throw<BusinessException>(() => BankLedgerCoding.NextCode(allUsed))
            .Code.ShouldBe(SettlementBankAccountErrorCodes.SubAccountRangeExhausted);
    }

    [Theory]
    [InlineData("1101", true)]
    [InlineData("1149", true)]
    [InlineData("1100", false)] // the parent is NOT a sub-account
    [InlineData("1200", false)]
    [InlineData("1150", false)] // out of reserved range
    [InlineData("1099", false)]
    [InlineData("110", false)]
    [InlineData("11010", false)]
    [InlineData(null, false)]
    public void IsBankSubAccount_Recognizes_Only_The_Reserved_Range(string? code, bool expected)
    {
        BankLedgerCoding.IsBankSubAccount(code).ShouldBe(expected);
    }

    [Fact]
    public void Bank_SubAccounts_Are_Postable_But_Not_Part_Of_The_Canonical_Chart()
    {
        // Validated for posting…
        SettlementAccountCode.IsPostable("1101").ShouldBeTrue();
        // …but the canonical chart is unchanged (still the 11 signed-off accounts).
        SettlementAccountCode.IsDefined("1101").ShouldBeFalse();
        SettlementChartOfAccounts.All.Count.ShouldBe(11);
        SettlementChartOfAccounts.All.Any(a => a.Code == "1101").ShouldBeFalse();
    }

    [Theory]
    [InlineData("SA4420000001234567891234", "••••••••••••••••••••1234")]
    [InlineData("1234567890", "••••••7890")]
    [InlineData("7890", "7890")]
    [InlineData("90", "90")]
    public void Mask_Keeps_Last_Four(string input, string expected)
    {
        BankLedgerCoding.Mask(input).ShouldBe(expected);
    }
}
