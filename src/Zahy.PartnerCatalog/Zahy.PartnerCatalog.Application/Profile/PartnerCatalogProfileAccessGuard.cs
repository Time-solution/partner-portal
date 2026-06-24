using System;
using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Services;
using Zahy.Identity.Partners;
using Zahy.Identity.Permissions;

namespace Zahy.PartnerCatalog.Profile;

/// <summary>
/// Phase 6a — access control for the partner catalog-side presentation profile (PartnerBrief). Unlike the
/// offering/package authoring-by-type rule, the brief is self-description (NOT pricing) so it is editable
/// by the partner for ALL partner types on their OWN profile, or by an admin (manages-all) for any partner.
/// Cross-partner self-editing is blocked.
/// </summary>
public class PartnerCatalogProfileAccessGuard : DomainService
{
    private readonly ICurrentPartner _currentPartner;
    private readonly IPermissionChecker _permissionChecker;

    public PartnerCatalogProfileAccessGuard(
        ICurrentPartner currentPartner,
        IPermissionChecker permissionChecker)
    {
        _currentPartner = currentPartner;
        _permissionChecker = permissionChecker;
    }

    public async Task EnsureCanEditBriefAsync(Guid partnerId)
    {
        // Admin (manages-all) may edit any partner's brief.
        if (await _permissionChecker.IsGrantedAsync(ZahyPermissions.Catalog.AuthorManaged))
        {
            return;
        }

        // A partner may edit their OWN brief regardless of partner type (self-description, not pricing).
        if (await _permissionChecker.IsGrantedAsync(ZahyPermissions.Catalog.AuthorSelf))
        {
            if (_currentPartner.Id == null || _currentPartner.Id.Value != partnerId)
            {
                throw new AbpAuthorizationException("Partner brief editing is limited to your own partner.");
            }

            return;
        }

        throw new AbpAuthorizationException("Catalog authoring permission required.");
    }
}
