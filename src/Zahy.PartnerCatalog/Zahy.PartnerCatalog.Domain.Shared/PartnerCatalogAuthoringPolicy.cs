using System.Linq;
using Volo.Abp;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Phase 1 authoring rules — service/subscription partners self-author tiers;
/// delivery/fulfilment/carrier/3PL purchase agreements are admin-managed only.
/// </summary>
public static class PartnerCatalogAuthoringPolicy
{
    public static bool IsSelfServiceOfferingKind(PartnerCatalogOfferingKind kind) =>
        kind is PartnerCatalogOfferingKind.ServiceOneOff
            or PartnerCatalogOfferingKind.ServiceSubscription;

    public static bool IsAdminManagedOfferingKind(PartnerCatalogOfferingKind kind) =>
        !IsSelfServiceOfferingKind(kind);

    public static bool IsSelfServicePartnerType(PartnerType partnerType) =>
        partnerType == PartnerType.Service;

    public static bool IsAdminManagedPartnerType(PartnerType partnerType) =>
        !IsSelfServicePartnerType(partnerType);

    public static void EnsureKindAllowedForPartnerType(
        PartnerType partnerType,
        PartnerCatalogOfferingKind offeringKind)
    {
        var suggested = PartnerCatalogOfferingKindDefaults.GetSuggestedKinds(partnerType);
        if (!suggested.Contains(offeringKind))
        {
            throw new BusinessException(PartnerCatalogErrorCodes.OfferingKindNotAllowedForPartnerType)
                .WithData("PartnerType", partnerType.ToString())
                .WithData("OfferingKind", offeringKind.ToString());
        }
    }

    public static void EnsureSelfAuthorAllowed(
        PartnerType partnerType,
        PartnerCatalogOfferingKind offeringKind)
    {
        if (!IsSelfServicePartnerType(partnerType))
        {
            throw new BusinessException(PartnerCatalogErrorCodes.AuthoringNotPermitted)
                .WithData("Reason", "ManagedPartnerTypeRequiresAdminAuthoring")
                .WithData("PartnerType", partnerType.ToString());
        }

        if (!IsSelfServiceOfferingKind(offeringKind))
        {
            throw new BusinessException(PartnerCatalogErrorCodes.AuthoringNotPermitted)
                .WithData("Reason", "SelfServicePartnersMayOnlyAuthorServiceOfferings")
                .WithData("OfferingKind", offeringKind.ToString());
        }
    }
}
