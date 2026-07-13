namespace Zahy.PartnerCatalog;

/// <summary>
/// Gate 2a — structured listing schema. What a merchant must provide (requirements), what they get
/// (deliverables), how it runs (steps), the terms, and FAQs. Presentation/config ONLY — never an
/// order-lifecycle, money, or settlement input. FileUpload is a requirement TYPE the merchant will
/// satisfy at order time (Gate 2b); authoring only names it here.
/// </summary>
public enum ListingRequirementType
{
    ShortText = 1,
    LongText = 2,
    FileUpload = 3,
    Link = 4,
    MultiChoice = 5
}

public static class PartnerCatalogListingConsts
{
    public const int MaxRequirements = 10;
    public const int MaxDeliverables = 5;
    public const int MaxExecutionSteps = 10;
    public const int MaxTerms = 10;
    public const int MaxFaqs = 10;
    public const int MaxChoicesPerRequirement = 8;

    public const int MaxRequirementTitleLength = 150;
    public const int MaxChoiceLength = 80;
    public const int MaxDeliverableTitleLength = 150;
    public const int MaxExecutionStepLength = 300;
    public const int MaxTermLength = 300;
    public const int MaxFaqQuestionLength = 200;
    public const int MaxFaqAnswerLength = 500;

    public const int MinDeliverableQuantity = 1;
    public const int MaxDeliverableQuantity = 99;
}

/// <summary>
/// Listing error codes — range :050–:059 in the Zahy.PartnerCatalog namespace.
/// Range evidence at assignment time: :001–:038 are occupied contiguously (all in
/// PartnerCatalogConsts.cs, no duplicates); nothing exists at :039+. :050+ is taken with a
/// :039–:049 buffer — deliberately avoiding adjacent reuse, the defect class behind the known
/// Zahy.Settlement Payment/Vat :030–:033 collision (a DIFFERENT namespace, listed here only as
/// the pattern to avoid).
/// </summary>
public static class PartnerCatalogListingErrorCodes
{
    public const string Namespace = "Zahy.PartnerCatalog";

    /// <summary>A section holds more rows than its cap.</summary>
    public const string ListingRowCapExceeded = Namespace + ":050";

    /// <summary>A row field exceeds its length cap (field named in the error data).</summary>
    public const string ListingFieldTooLong = Namespace + ":051";

    /// <summary>A required row field (title / text / question / answer) is empty.</summary>
    public const string ListingFieldRequired = Namespace + ":052";

    /// <summary>Choices supplied on a requirement whose type is not MultiChoice.</summary>
    public const string ListingChoicesOnlyForMultiChoice = Namespace + ":053";

    /// <summary>A MultiChoice requirement has no choices or more than the cap.</summary>
    public const string ListingChoicesInvalid = Namespace + ":054";

    /// <summary>Deliverable quantity outside 1–99.</summary>
    public const string ListingDeliverableQuantityOutOfRange = Namespace + ":055";

    /// <summary>Merchant-facing text may not carry URLs, emails, or phone numbers
    /// (anti-disintermediation default — ratifiable policy).</summary>
    public const string MerchantFacingContactInfoNotAllowed = Namespace + ":056";
}
