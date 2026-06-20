using System;
using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Zahy.Identity.Permissions;

namespace Zahy.PartnerCatalog.Merchant;

public class MerchantCatalogAccessGuard : DomainService
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IPermissionChecker _permissionChecker;

    public MerchantCatalogAccessGuard(
        ICurrentTenant currentTenant,
        IPermissionChecker permissionChecker)
    {
        _currentTenant = currentTenant;
        _permissionChecker = permissionChecker;
    }

    public async Task<Guid> GetRequiredMerchantTenantIdAsync()
    {
        if (_currentTenant.Id != null)
        {
            return _currentTenant.Id.Value;
        }

        if (await _permissionChecker.IsGrantedAsync(ZahyPermissions.Partners.Manage))
        {
            throw new AbpAuthorizationException("Merchant tenant context is required.");
        }

        throw new AbpAuthorizationException("Merchant tenant context is required.");
    }

    public async Task EnsureCanAccessTenantAsync(Guid tenantId)
    {
        if (_currentTenant.Id != null)
        {
            if (_currentTenant.Id.Value != tenantId)
            {
                throw new AbpAuthorizationException("Merchant access denied.");
            }

            return;
        }

        if (await _permissionChecker.IsGrantedAsync(ZahyPermissions.Partners.Manage))
        {
            return;
        }

        throw new AbpAuthorizationException("Merchant access denied.");
    }

    public Task EnsureCanMutateActivationAsync(MerchantActivation activation) =>
        EnsureCanAccessTenantAsync(activation.TenantId!.Value);
}
