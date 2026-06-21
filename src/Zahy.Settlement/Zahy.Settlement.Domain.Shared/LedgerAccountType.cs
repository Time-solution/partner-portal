namespace Zahy.Settlement;

/// <summary>
/// Top-level classification of a chart-of-accounts line. Determines the conventional normal side:
/// Asset and Expense are debit-normal; Liability and Revenue are credit-normal.
/// </summary>
public enum LedgerAccountType
{
    Asset = 1,
    Liability = 2,
    Revenue = 3,
    Expense = 4
}
