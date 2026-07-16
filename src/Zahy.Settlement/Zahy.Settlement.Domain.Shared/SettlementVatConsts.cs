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

    /// <summary>AF3 quarantine — KSA is PRINCIPAL-only; the dormant Agent VAT branch must never be
    /// reached from production (config, book mapping, or otherwise). Vacant code (:035 — clear of the
    /// known :030–:033 / :040–:042 / :061 collisions).</summary>
    public const string AgentTreatmentNotSupportedInKsa = Namespace + ":035";
}
