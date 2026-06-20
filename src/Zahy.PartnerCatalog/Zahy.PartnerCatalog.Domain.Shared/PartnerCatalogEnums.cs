namespace Zahy.PartnerCatalog;

public enum PartnerCatalogOfferingKind
{
    ServiceOneOff = 1,
    ServiceSubscription = 2,
    DeliveryFulfilmentPerOrder = 3,
    FnBItemsPerSale = 4,

    /// <summary>
    /// noon JUMP "Fulfilled by" shape — partner physically holds the merchant's stock in their
    /// warehouse, fulfils from there, then remits. The new element vs other kinds is stock CUSTODY.
    /// </summary>
    ConsignmentFulfilment = 5
}

/// <summary>
/// Who owns the stock the partner holds in custody (the one new commercial decision for
/// <see cref="PartnerCatalogOfferingKind.ConsignmentFulfilment"/>). It only routes to an EXISTING
/// settlement participation mode — no new settlement logic. Default is <see cref="MerchantOwned"/>
/// (true consignment). Final mode confirmed with accountant at go-live, like other flows.
/// </summary>
public enum ConsignmentOwnershipMode
{
    /// <summary>Consignment — partner sells the merchant's goods and remits. Settles ReflectionOnly.</summary>
    MerchantOwned = 1,

    /// <summary>Partner bought the goods up-front and owns them. Settles Principal.</summary>
    PartnerBought = 2
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
