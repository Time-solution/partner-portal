namespace Zahy.PartnerCatalog;

public static class PartnerCatalogConsts
{
    public const string DefaultCurrency = Settlement.SettlementConsts.DefaultCurrency;

    public const int MaxCodeLength = 64;
    public const int MaxNameLength = 256;
    public const int MaxDescriptionLength = 2048;

    // Phase 6a — partner authoring presentation (plain text, optional, not required to publish).
    public const int MaxPartnerBriefLength = 600;
    public const int MaxMerchantBenefitLength = 400;
    public const int MaxPackageExplanationLength = 500;
    public const int MaxCarrierServiceCodeLength = 64;
    public const int MaxExternalMenuItemIdLength = 128;
    public const int MaxMenuCategoryCodeLength = 64;
    public const int MaxExternalTransactionIdLength = 256;
    public const int MaxOrderLineIdLength = 128;
    public const int MaxShape2HandshakeVersionLength = 64;
    public const int MaxSyncErrorLength = 2048;
    public const int MaxIdempotencyKeyLength = 256;
    public const int MaxExternalReferenceLength = 128;
}

public static class PartnerCatalogErrorCodes
{
    public const string Namespace = "Zahy.PartnerCatalog";

    public const string InvalidPartnerCost = Namespace + ":001";
    public const string InvalidStatusTransition = Namespace + ":002";
    public const string InvalidOfferingKind = Namespace + ":003";
    public const string ItemNotActive = Namespace + ":004";
    public const string InvalidReflectionWindow = Namespace + ":005";
    public const string EmptyExternalTransactionId = Namespace + ":006";
    public const string MissingOrderLineId = Namespace + ":007";
    public const string InvalidResalePrice = Namespace + ":008";
    public const string InvalidActivation = Namespace + ":009";
    public const string CatalogItemMismatch = Namespace + ":010";
    public const string InvalidSettlementLink = Namespace + ":011";
    public const string SettlementCaseAlreadyLinked = Namespace + ":012";
    public const string InvalidBillingLink = Namespace + ":013";
    public const string BillingChargeAlreadyLinked = Namespace + ":014";
    public const string InvalidSubscriptionFee = Namespace + ":015";
    /// <summary>RETIRED (CAT-FIX): re-activation after End is ratified ALLOWED via sequence-suffixed
    /// idempotency keys — no production path throws this anymore. Code stays reserved; do not reuse :016.</summary>
    public const string ActivationAlreadyEnded = Namespace + ":016";
    public const string ActivationNotFound = Namespace + ":017";
    public const string AuthoringNotPermitted = Namespace + ":018";
    public const string OfferingKindNotAllowedForPartnerType = Namespace + ":019";
    public const string DuplicateCatalogCode = Namespace + ":020";
    public const string CatalogItemNotFound = Namespace + ":021";
    public const string CannotUpdateNonDraft = Namespace + ":022";
    public const string PartnerNotFound = Namespace + ":023";

    // U2 — usage package config/authoring.
    public const string UsagePackageNotFound = Namespace + ":024";
    public const string InvalidUsagePackage = Namespace + ":025";
    public const string InvalidUsagePackageStatusTransition = Namespace + ":026";

    // U4 — merchant package selection / activation link.
    public const string UsagePackageNotPublished = Namespace + ":027";
    public const string UsagePackageSelectionNotFound = Namespace + ":028";
    public const string UsagePackageSelectionAlreadyEnded = Namespace + ":029";
    public const string InvalidUsagePackageSelection = Namespace + ":030";

    // U5 — optional volume tiers on a package's overage.
    public const string InvalidUsagePackageTier = Namespace + ":031";

    // R1 — partner<->merchant participation LINK (the data-sharing relationship: partner GRANT +
    // merchant CONSENT, PDPL). Contiguous free range :032–:037 (verified no collision with :001–:031).
    public const string InvalidPartnerMerchantLink = Namespace + ":032";
    public const string GrantNotOffered = Namespace + ":033";
    public const string StaleConsentVersion = Namespace + ":034";
    public const string PartnerMerchantLinkTerminal = Namespace + ":035";
    public const string InvalidLinkStateTransition = Namespace + ":036";
    public const string StandingGrantRequired = Namespace + ":037";

    // Phase 6a — partner catalog-side presentation profile (PartnerBrief). Free slot :038 (no collision).
    public const string InvalidPartnerCatalogProfile = Namespace + ":038";
}
