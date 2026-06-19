namespace Zahy.Commission;

public static class CommissionErrorCodes
{
    public const string Namespace = "Zahy.Commission";

    public const string InvalidBasisAmount = Namespace + ":001";
    public const string InvalidCurrency = Namespace + ":002";
    public const string InvalidBasisDefinition = Namespace + ":003";
    public const string InvalidCommissionRule = Namespace + ":004";
    public const string InvalidStatusTransition = Namespace + ":010";
    public const string LedgerEntryNotFound = Namespace + ":011";
    public const string InvalidLedgerEntry = Namespace + ":012";
    public const string ReversalNotAllowed = Namespace + ":013";

    public const string InvalidBillingCharge = Namespace + ":020";
    public const string BillingProfileNotFound = Namespace + ":021";
}
