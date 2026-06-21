using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

public class SettlementChartOfAccountsTests
{
    [Fact]
    public void Chart_Has_Exactly_Eleven_Accounts()
    {
        SettlementChartOfAccounts.All.Count.ShouldBe(11);
    }

    [Fact]
    public void Codes_Are_Unique()
    {
        var codes = SettlementChartOfAccounts.All.Select(a => a.Code).ToList();
        codes.Distinct().Count().ShouldBe(codes.Count);
    }

    [Theory]
    [InlineData("1100", "Bank / Cash Clearing", LedgerAccountType.Asset, EntryDirection.Debit)]
    [InlineData("1200", "Accounts Receivable - Merchant", LedgerAccountType.Asset, EntryDirection.Debit)]
    [InlineData("1250", "Accounts Receivable - Partner", LedgerAccountType.Asset, EntryDirection.Debit)]
    [InlineData("1300", "Input VAT Recoverable", LedgerAccountType.Asset, EntryDirection.Debit)]
    [InlineData("2100", "Accounts Payable - Partner", LedgerAccountType.Liability, EntryDirection.Credit)]
    [InlineData("2200", "Output VAT Payable", LedgerAccountType.Liability, EntryDirection.Credit)]
    [InlineData("2300", "VAT Control (period close only)", LedgerAccountType.Liability, EntryDirection.Credit)]
    [InlineData("2400", "Reflection / Pass-through Clearing", LedgerAccountType.Liability, EntryDirection.Credit)]
    [InlineData("4100", "Resale Revenue", LedgerAccountType.Revenue, EntryDirection.Credit)]
    [InlineData("4200", "Subscription / Fee Revenue", LedgerAccountType.Revenue, EntryDirection.Credit)]
    [InlineData("5100", "Partner Purchase Cost (COGS)", LedgerAccountType.Expense, EntryDirection.Debit)]
    public void Account_Has_Expected_Type_And_NormalSide(
        string code,
        string name,
        LedgerAccountType type,
        EntryDirection normalSide)
    {
        var account = SettlementChartOfAccounts.All.SingleOrDefault(a => a.Code == code);

        account.ShouldNotBeNull();
        account!.Name.ShouldBe(name);
        account.Type.ShouldBe(type);
        account.NormalSide.ShouldBe(normalSide);
    }

    [Fact]
    public void NormalSide_Is_Consistent_With_AccountType()
    {
        foreach (var account in SettlementChartOfAccounts.All)
        {
            var expected = account.Type is LedgerAccountType.Asset or LedgerAccountType.Expense
                ? EntryDirection.Debit
                : EntryDirection.Credit;

            account.NormalSide.ShouldBe(expected);
        }
    }
}
