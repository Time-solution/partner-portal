using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Services;
using Zahy.Identity.Partners;
using Zahy.Identity.Permissions;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.PartnerCatalog.Write;

public class PartnerCatalogWriteAccessGuard : DomainService
{
    private readonly ICurrentPartner _currentPartner;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IPartnerCatalogPartnerTypeLookup _partnerTypeLookup;

    public PartnerCatalogWriteAccessGuard(
        ICurrentPartner currentPartner,
        IPermissionChecker permissionChecker,
        IPartnerCatalogPartnerTypeLookup partnerTypeLookup)
    {
        _currentPartner = currentPartner;
        _permissionChecker = permissionChecker;
        _partnerTypeLookup = partnerTypeLookup;
    }

    public async Task EnsureCanAuthorAsync(Guid partnerId, PartnerCatalogOfferingKind offeringKind)
    {
        var partnerType = await RequirePartnerTypeAsync(partnerId);

        if (await _permissionChecker.IsGrantedAsync(ZahyPermissions.Catalog.AuthorManaged))
        {
            PartnerCatalogAuthoringPolicy.EnsureKindAllowedForPartnerType(partnerType, offeringKind);
            return;
        }

        if (await _permissionChecker.IsGrantedAsync(ZahyPermissions.Catalog.AuthorSelf))
        {
            if (_currentPartner.Id == null || _currentPartner.Id.Value != partnerId)
            {
                throw new AbpAuthorizationException("Partner catalog authoring is limited to your own partner.");
            }

            PartnerCatalogAuthoringPolicy.EnsureSelfAuthorAllowed(partnerType, offeringKind);
            return;
        }

        throw new AbpAuthorizationException("Catalog authoring permission required.");
    }

    public async Task EnsureCanMutateItemAsync(PartnerCatalogItem item) =>
        await EnsureCanAuthorAsync(item.PartnerId, item.OfferingKind);

    private async Task<PartnerType> RequirePartnerTypeAsync(Guid partnerId)
    {
        var partnerType = await _partnerTypeLookup.GetPartnerTypeAsync(partnerId);
        if (partnerType == null)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.PartnerNotFound)
                .WithData("PartnerId", partnerId);
        }

        return partnerType.Value;
    }
}
