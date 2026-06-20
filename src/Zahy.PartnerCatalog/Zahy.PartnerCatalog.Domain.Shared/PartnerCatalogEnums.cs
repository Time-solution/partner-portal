namespace Zahy.PartnerCatalog;

public enum PartnerCatalogOfferingKind
{
    ServiceOneOff = 1,
    ServiceSubscription = 2,
    DeliveryFulfilmentPerOrder = 3,
    FnBItemsPerSale = 4
}

public enum PartnerCatalogItemStatus
{
    Draft = 1,
    Active = 2,
    Archived = 3
}

public enum PartnerCatalogFulfilmentUnit
{
    PerShipment = 1
}

public enum PartnerCatalogReflectionAudience
{
    AllMerchants = 1
}

public enum SettlementTriggerMode
{
    OnActivation = 1,
    PerOrder = 2,
    PerBillingPeriod = 3,
    PerOrderLine = 4
}

public enum SettlementCostMarkupTrigger
{
    Activation = 1,
    Order = 2,
    OrderLine = 3,
    BillingPeriod = 4
}

public enum PartnerCatalogSellPriceSource
{
    /// <summary>FnB sell leg not yet chosen — accountant decision pending (DESIGN §11 A2).</summary>
    Unresolved = 0,
    ListingResalePrice = 1,
    ActualOrderLinePrice = 2
}

public enum PlatformCatalogLinkStatus
{
    NotApplicable = 0,
    PendingSync = 1,
    Synced = 2,
    SyncFailed = 3,
    DeferredShape2 = 4
}

public enum MerchantActivationStatus
{
    Pending = 1,
    Active = 2,
    Suspended = 3,
    Ended = 4
}

/// <summary>How settlement participation is routed — stored now; bridge dispatch in 2b/2c.</summary>
public enum SettlementParticipationMode
{
    Principal = 1,
    ReflectionOnly = 2,
    SubscriptionFee = 3
}
