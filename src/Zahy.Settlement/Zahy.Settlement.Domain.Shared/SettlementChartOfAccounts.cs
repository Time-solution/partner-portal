using System.Collections.Generic;

namespace Zahy.Settlement;

/// <summary>
/// The canonical settlement Chart of Accounts (Phase A). Single source of truth for both the
/// data seed and the chart tests. Normal side follows the account type (Asset/Expense → Debit;
/// Liability/Revenue → Credit). Codes are unique and shared across the partner-type portfolios.
/// </summary>
public static class SettlementChartOfAccounts
{
    public static IReadOnlyList<ChartAccountDefinition> All { get; } = new[]
    {
        new ChartAccountDefinition("1100", "Bank / Cash Clearing", LedgerAccountType.Asset, EntryDirection.Debit, AccountPortfolio.Shared),
        new ChartAccountDefinition("1200", "Accounts Receivable - Merchant", LedgerAccountType.Asset, EntryDirection.Debit, AccountPortfolio.Shared),
        new ChartAccountDefinition("1250", "Accounts Receivable - Partner", LedgerAccountType.Asset, EntryDirection.Debit, AccountPortfolio.Shared),
        new ChartAccountDefinition("1300", "Input VAT Recoverable", LedgerAccountType.Asset, EntryDirection.Debit, AccountPortfolio.Shared),
        new ChartAccountDefinition("2100", "Accounts Payable - Partner", LedgerAccountType.Liability, EntryDirection.Credit, AccountPortfolio.Shared),
        new ChartAccountDefinition("2200", "Output VAT Payable", LedgerAccountType.Liability, EntryDirection.Credit, AccountPortfolio.Shared),
        new ChartAccountDefinition("2300", "VAT Control (period close only)", LedgerAccountType.Liability, EntryDirection.Credit, AccountPortfolio.Shared),
        new ChartAccountDefinition("2400", "Reflection / Pass-through Clearing", LedgerAccountType.Liability, EntryDirection.Credit, AccountPortfolio.Shared),
        new ChartAccountDefinition("4100", "Resale Revenue", LedgerAccountType.Revenue, EntryDirection.Credit, AccountPortfolio.Shared),
        new ChartAccountDefinition("4200", "Subscription / Fee Revenue", LedgerAccountType.Revenue, EntryDirection.Credit, AccountPortfolio.Shared),
        new ChartAccountDefinition("5100", "Partner Purchase Cost (COGS)", LedgerAccountType.Expense, EntryDirection.Debit, AccountPortfolio.Shared),
    };
}

/// <summary>A seedable chart-of-accounts row (code + name + classification + normal side + portfolio).</summary>
public sealed record ChartAccountDefinition(
    string Code,
    string Name,
    LedgerAccountType Type,
    EntryDirection NormalSide,
    AccountPortfolio Portfolio);
