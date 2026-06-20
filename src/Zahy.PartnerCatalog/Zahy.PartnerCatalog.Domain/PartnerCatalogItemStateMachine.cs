namespace Zahy.PartnerCatalog;

public static class PartnerCatalogItemStateMachine
{
    public static bool CanTransition(PartnerCatalogItemStatus from, PartnerCatalogItemStatus to) =>
        (from, to) switch
        {
            (PartnerCatalogItemStatus.Draft, PartnerCatalogItemStatus.Active) => true,
            (PartnerCatalogItemStatus.Draft, PartnerCatalogItemStatus.Archived) => true,
            (PartnerCatalogItemStatus.Active, PartnerCatalogItemStatus.Archived) => true,
            _ => false
        };
}
