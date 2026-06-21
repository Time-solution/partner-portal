namespace Zahy.Settlement;

/// <summary>Who an invoice is addressed to (the party that OWES Zahy — a receivable).</summary>
public enum InvoiceEntityType
{
    Merchant = 1,
    Partner = 2
}

/// <summary>How a billing line is charged.</summary>
public enum BillingType
{
    /// <summary>A recurring monthly fee (prorated by calendar days if deactivated mid-period).</summary>
    Subscription = 1,

    /// <summary>A per-successful-transaction fee (count × per-txn fee, from the bulk-invoice read model).</summary>
    PerTransaction = 2
}
