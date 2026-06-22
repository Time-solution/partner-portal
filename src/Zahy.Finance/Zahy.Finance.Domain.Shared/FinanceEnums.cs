namespace Zahy.Finance;

public enum FinanceAccountStatus
{
    Pending = 1,
    Active = 2,
    Suspended = 3,
    Closed = 4
}

public enum FinanceAccountKind
{
    Partner = 1,
    Merchant = 2
}

public enum KycEntityKind
{
    Partner = 1,
    Merchant = 2
}

public enum KycVerificationStatus
{
    Submitted = 1,
    UnderReview = 2,
    Verified = 3,
    Rejected = 4
}

public enum FinancePostingSourceModule
{
    Commission = 1,
    Billing = 2,
    Finance = 3
}

public enum FinanceExportFormat
{
    Csv = 1,
    Xlsx = 2
}

public enum FinanceDocumentKind
{
    Statement = 1,
    Invoice = 2,
    TransactionExport = 3,
    CommissionExport = 4
}

public enum InvoiceGenerationMode
{
    Manual = 1,
    Automatic = 2
}

/// <summary>How a finance document was produced. LedgerDerived is the existing ledger-sourced generator;
/// Manual is an ad-hoc invoice keyed in by an accountant with its own line items.</summary>
public enum FinanceDocumentSource
{
    LedgerDerived = 1,
    Manual = 2
}

/// <summary>Who a manual invoice is addressed to.</summary>
public enum FinanceDocumentRecipientType
{
    Partner = 1,
    Merchant = 2,
    External = 3
}
