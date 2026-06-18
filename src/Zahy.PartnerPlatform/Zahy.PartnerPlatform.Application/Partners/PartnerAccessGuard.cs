using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Services;
using Zahy.Identity.Partners;
using Zahy.Identity.Permissions;

namespace Zahy.PartnerPlatform.Partners;

/// <summary>Enforces partner aggregate isolation for partner-scoped operations.</summary>
public class PartnerAccessGuard : DomainService
{
    private readonly ICurrentPartner _currentPartner;
    private readonly IPermissionChecker _permissionChecker;

    public PartnerAccessGuard(ICurrentPartner currentPartner, IPermissionChecker permissionChecker)
    {
        _currentPartner = currentPartner;
        _permissionChecker = permissionChecker;
    }

    public async Task EnsureCanAccessPartnerAsync(Guid partnerId)
    {
        if (_currentPartner.Id != null)
        {
            if (_currentPartner.Id != partnerId)
            {
                throw new AbpAuthorizationException("Partner access denied.");
            }

            return;
        }

        if (await _permissionChecker.IsGrantedAsync(ZahyPermissions.Partners.Manage))
        {
            return;
        }

        throw new AbpAuthorizationException("Partner access denied.");
    }

    public Task<Guid> GetRequiredPartnerIdAsync()
    {
        if (_currentPartner.Id == null)
        {
            throw new AbpAuthorizationException("Partner context is required.");
        }

        return Task.FromResult(_currentPartner.Id.Value);
    }
}
