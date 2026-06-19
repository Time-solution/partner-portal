namespace Zahy.Finance;

public static class FinanceConsts
{
    public const string DefaultCurrency = "SAR";

    public const int MaxIdempotencyKeyLength = 512;
    public const int MaxDescriptionLength = 512;
    public const int MaxSourceTypeLength = 64;
    public const int MaxSourceIdLength = 256;
}

public static class FinanceMoney
{
    public const int PostingScale = 2;

    public static decimal RoundPosting(decimal value) =>
        Math.Round(value, PostingScale, MidpointRounding.AwayFromZero);
}

public static class FinancePostingIdempotency
{
    public static string BuildCommissionKey(Guid ledgerEntryId) =>
        $"commission:accrual:{ledgerEntryId:N}";

    public static string BuildBillingKey(Guid billingChargeId) =>
        $"billing:charge:{billingChargeId:N}";
}

public static class FinanceErrorCodes
{
    public const string Namespace = "Zahy.Finance";

    public const string KycNotVerified = Namespace + ":001";
    public const string AccountNotFound = Namespace + ":002";
    public const string AccountNotActive = Namespace + ":003";
    public const string InvalidPosting = Namespace + ":004";
    public const string AccessDenied = Namespace + ":005";
    public const string IllegalKycTransition = Namespace + ":006";
    public const string KycVerificationNotFound = Namespace + ":007";
    public const string KycSubmissionNotFound = Namespace + ":008";
}

public static class FinanceKycConsts
{
    public const int MaxLegalNameLength = 256;
    public const int MaxCrNumberLength = 32;
    public const int MaxVatNumberLength = 32;
    public const int MaxIbanLength = 34;
    public const int MaxAddressLength = 512;
    public const int MaxReviewNotesLength = 2000;
    public const int MaxProtectedFieldLength = 2048;
}
