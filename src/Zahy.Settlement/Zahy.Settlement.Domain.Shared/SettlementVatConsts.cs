namespace Zahy.Settlement;

/// <summary>Phase-3 (allocation/VAT) error codes.</summary>
public static class SettlementVatErrorCodes
{
    public const string Namespace = "Zahy.Settlement";

    public const string InvalidVatRate = Namespace + ":030";
    public const string PriceMustBeVatInclusive = Namespace + ":031";
    public const string PriceCurrencyMismatch = Namespace + ":032";
    public const string UnknownVatTreatment = Namespace + ":033";
    public const string TreatmentNotConfigured = Namespace + ":034";
}
