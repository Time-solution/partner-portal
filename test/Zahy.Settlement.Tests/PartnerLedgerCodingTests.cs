using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Per-partner ledger coding — the partner analogue of <see cref="BankLedgerCodingTests"/>. Payable
/// sub-accounts (2101–2149) sit under the 2100 parent and receivable sub-accounts (1251–1299) under the
/// 1250 parent. The codes are dynamic registry data: postable but NOT part of the canonical chart, so
/// the LIVE chart count is unchanged (the live chart change stays gated, default OFF).
/// </summary>
public class PartnerLedgerCodingTests
{
    [Fact]
    public void Parents_Are_The_Canonical_2100_And_1250()
    {
        PartnerLedgerCoding.PayableParentCode.ShouldBe(SettlementAccountCode.ApPartner);
        PartnerLedgerCoding.PayableParentCode.ShouldBe("2100");
        PartnerLedgerCoding.ReceivableParentCode.ShouldBe(SettlementAccountCode.ArPartner);
        PartnerLedgerCoding.ReceivableParentCode.ShouldBe("1250");
    }

    [Fact]
    public void NextPayableCode_Assigns_2101_Upwards_And_Reuses_Gaps()
    {
        PartnerLedgerCoding.NextPayableCode(Array.Empty<string>()).ShouldBe("2101");
        PartnerLedgerCoding.NextPayableCode(new[] { "2101" }).ShouldBe("2102");
        PartnerLedgerCoding.NextPayableCode(new[] { "2101", "2103" }).ShouldBe("2102");
    }

    [Fact]
    public void NextReceivableCode_Assigns_1251_Upwards_And_Reuses_Gaps()
    {
        PartnerLedgerCoding.NextReceivableCode(Array.Empty<string>()).ShouldBe("1251");
        PartnerLedgerCoding.NextReceivableCode(new[] { "1251" }).ShouldBe("1252");
        PartnerLedgerCoding.NextReceivableCode(new[] { "1251", "1253" }).ShouldBe("1252");
    }

    [Fact]
    public void NextCode_Throws_When_The_Reserved_Range_Is_Exhausted()
    {
        var allPayable = Enumerable
            .Range(PartnerLedgerCoding.FirstPayableSubCode,
                PartnerLedgerCoding.LastPayableSubCode - PartnerLedgerCoding.FirstPayableSubCode + 1)
            .Select(n => n.ToString())
            .ToArray();

        Should.Throw<BusinessException>(() => PartnerLedgerCoding.NextPayableCode(allPayable))
            .Code.ShouldBe(SettlementPartnerLedgerErrorCodes.SubAccountRangeExhausted);
    }

    [Theory]
    [InlineData("2101", true)]
    [InlineData("2149", true)]
    [InlineData("2150", false)]
    [InlineData("2200", false)] // Output VAT — not a partner sub-account
    [InlineData("2100", false)] // the parent itself is not a sub-account
    [InlineData("1251", false)] // receivable, not payable
    [InlineData(null, false)]
    public void IsPayableSubAccount_Recognizes_Only_2101_To_2149(string? code, bool expected)
    {
        PartnerLedgerCoding.IsPayableSubAccount(code).ShouldBe(expected);
    }

    [Theory]
    [InlineData("1251", true)]
    [InlineData("1299", true)]
    [InlineData("1300", false)] // Input VAT — not a partner sub-account
    [InlineData("1250", false)] // the parent itself is not a sub-account
    [InlineData("2101", false)] // payable, not receivable
    [InlineData(null, false)]
    public void IsReceivableSubAccount_Recognizes_Only_1251_To_1299(string? code, bool expected)
    {
        PartnerLedgerCoding.IsReceivableSubAccount(code).ShouldBe(expected);
    }

    [Fact]
    public void Partner_Sub_Accounts_Are_Postable_But_Not_Canonical_Chart_Entries()
    {
        // Postable (a posting may target them)…
        SettlementAccountCode.IsPostable("2101").ShouldBeTrue();
        SettlementAccountCode.IsPostable("1251").ShouldBeTrue();

        // …but NOT defined in the canonical chart — they are dynamic registry data.
        SettlementAccountCode.IsDefined("2101").ShouldBeFalse();
        SettlementAccountCode.IsDefined("1251").ShouldBeFalse();

        // The LIVE chart is unchanged: still 11 canonical accounts, no 21xx/12xx sub-codes seeded.
        SettlementChartOfAccounts.All.Count.ShouldBe(11);
        SettlementChartOfAccounts.All.Any(a => a.Code == "2101").ShouldBeFalse();
        SettlementChartOfAccounts.All.Any(a => a.Code == "1251").ShouldBeFalse();
    }

    [Fact]
    public void Live_Chart_Change_Stays_Gated_Off_By_Default()
    {
        new SettlementEngineOptions().PartnerLedgerLiveChartEnabled.ShouldBeFalse();
    }
}
