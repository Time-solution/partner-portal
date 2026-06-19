namespace Zahy.Commission;

public static class BillingConsts
{
    public const int MaxPeriodKeyLength = 32;
    public const int MaxDescriptionLength = 512;
    public const int MaxIdempotencyKeyLength = 512;
}

public static class BillingIdempotency
{
    public static string BuildPeriodKey(Guid partnerId, string period) =>
        $"{partnerId:N}:{period}";

    public static string BuildActivationKey(Guid partnerId) =>
        $"{partnerId:N}:activation";

    public static string BuildCommissionTransactionKey(Guid partnerId, Guid commissionLedgerEntryId) =>
        $"{partnerId:N}:txn:commission:{commissionLedgerEntryId:N}";
}

public static class BillingSourceTypes
{
    public const string CommissionAccrual = "commission.accrual";
    public const string Subscription = "billing.subscription";
    public const string Activation = "billing.activation";
}
