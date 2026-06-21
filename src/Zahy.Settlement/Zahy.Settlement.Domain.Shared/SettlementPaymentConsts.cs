namespace Zahy.Settlement;

public static class SettlementPaymentConsts
{
    /// <summary>The settlement/invoice reference a payment is applied against.</summary>
    public const int MaxAgainstRefLength = 128;

    /// <summary>Optional free-text payment method (e.g. "bank-transfer", "mada").</summary>
    public const int MaxMethodLength = 64;
}

/// <summary>
/// Payment-received error codes. Kept separate from the Phase-1 <see cref="SettlementErrorCodes"/>
/// and Phase-2 <see cref="SettlementCaseErrorCodes"/> so the locked earlier files are not modified.
/// </summary>
public static class SettlementPaymentErrorCodes
{
    public const string Namespace = "Zahy.Settlement";

    /// <summary>A payment that would push cumulative receipts above the AR total is rejected.</summary>
    public const string Overpayment = Namespace + ":030";

    public const string NonPositivePayment = Namespace + ":031";

    public const string EmptyAgainstRef = Namespace + ":032";

    public const string PaymentCurrencyMismatch = Namespace + ":033";
}
