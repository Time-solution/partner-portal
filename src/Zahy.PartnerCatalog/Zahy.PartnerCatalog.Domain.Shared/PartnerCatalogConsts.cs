namespace Zahy.PartnerCatalog;

public static class PartnerCatalogConsts
{
    public const string DefaultCurrency = Settlement.SettlementConsts.DefaultCurrency;

    public const int MaxCodeLength = 64;
    public const int MaxNameLength = 256;
    public const int MaxDescriptionLength = 2048;
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
    public const string ActivationAlreadyEnded = Namespace + ":016";
    public const string ActivationNotFound = Namespace + ":017";
    public const string AuthoringNotPermitted = Namespace + ":018";
    public const string OfferingKindNotAllowedForPartnerType = Namespace + ":019";
    public const string DuplicateCatalogCode = Namespace + ":020";
    public const string CatalogItemNotFound = Namespace + ":021";
    public const string CannotUpdateNonDraft = Namespace + ":022";
    public const string PartnerNotFound = Namespace + ":023";
}
