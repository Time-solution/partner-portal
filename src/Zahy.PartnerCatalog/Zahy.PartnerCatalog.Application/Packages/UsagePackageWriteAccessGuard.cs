using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Services;
using Zahy.Identity.Partners;
using Zahy.Identity.Permissions;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.PartnerCatalog.Packages;

/// <summary>
/// U2 authoring access — reuses the catalog Phase-1 authoring-by-type + self-publish rules for usage
/// packages: admin (Catalog.AuthorManaged) authors ANY partner's packages; a service/subscription
/// partner (Catalog.AuthorSelf) self-publishes ONLY their own; delivery/3PL/carrier are admin-managed
/// and cannot self-author. Cross-partner self-authoring is blocked.
/// </summary>
public class UsagePackageWriteAccessGuard : DomainService
{
    private readonly ICurrentPartner _currentPartner;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IPartnerCatalogPartnerTypeLookup _partnerTypeLookup;

    public UsagePackageWriteAccessGuard(
        ICurrentPartner currentPartner,
        IPermissionChecker permissionChecker,
        IPartnerCatalogPartnerTypeLookup partnerTypeLookup)
    {
        _currentPartner = currentPartner;
        _permissionChecker = permissionChecker;
        _partnerTypeLookup = partnerTypeLookup;
    }

    public async Task EnsureCanAuthorAsync(Guid partnerId)
    {
        var partnerType = await RequirePartnerTypeAsync(partnerId);

        // Admin (manages-all) may author any partner's packages.
        if (await _permissionChecker.IsGrantedAsync(ZahyPermissions.Catalog.AuthorManaged))
        {
            return;
        }

        // Self-service: a service/subscription partner self-publishes only their OWN packages.
        if (await _permissionChecker.IsGrantedAsync(ZahyPermissions.Catalog.AuthorSelf))
        {
            if (_currentPartner.Id == null || _currentPartner.Id.Value != partnerId)
            {
                throw new AbpAuthorizationException("Usage package authoring is limited to your own partner.");
            }

            if (!PartnerCatalogAuthoringPolicy.IsSelfServicePartnerType(partnerType))
            {
                throw new BusinessException(PartnerCatalogErrorCodes.AuthoringNotPermitted)
                    .WithData("Reason", "ManagedPartnerTypeRequiresAdminAuthoring")
                    .WithData("PartnerType", partnerType.ToString());
            }

            return;
        }

        throw new AbpAuthorizationException("Usage package authoring permission required.");
    }

    public Task EnsureCanMutateAsync(UsagePackage package) => EnsureCanAuthorAsync(package.PartnerId);

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
