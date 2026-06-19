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
