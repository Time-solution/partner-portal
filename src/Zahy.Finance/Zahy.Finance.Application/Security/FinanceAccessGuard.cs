using System;
using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Zahy.Identity.Partners;
using Zahy.Identity.Permissions;

namespace Zahy.Finance;

public class FinanceAccessGuard : DomainService
{
    private readonly ICurrentPartner _currentPartner;
    private readonly ICurrentTenant _currentTenant;
    private readonly IPermissionChecker _permissionChecker;

    public FinanceAccessGuard(
        ICurrentPartner currentPartner,
        ICurrentTenant currentTenant,
        IPermissionChecker permissionChecker)
    {
        _currentPartner = currentPartner;
        _currentTenant = currentTenant;
        _permissionChecker = permissionChecker;
    }

    public async Task EnsureCanAccessPartnerAccountAsync(Guid partnerId)
    {
        // A partner principal is scoped to its OWN partner's payable side — never another partner's.
        if (_currentPartner.Id != null)
        {
            if (_currentPartner.Id != partnerId)
            {
                throw new AbpAuthorizationException("Partner access denied.");
            }

            return;
        }

        // Cross-entity "see all" is the accountant/admin Finance.ReadAll capability — NOT Partners.Manage.
        // Partner-ops (Partners.Manage) is partner administration, not finance, and must not read ledgers.
        if (await _permissionChecker.IsGrantedAsync(ZahyPermissions.Finance.ReadAll))
        {
            return;
        }

        throw new AbpAuthorizationException("Partner access denied.");
    }

    public async Task EnsureCanAccessMerchantAccountAsync(Guid tenantId)
    {
        // A merchant principal is scoped to its OWN tenant's ledger — never another merchant's.
        if (_currentTenant.Id != null)
        {
            if (_currentTenant.Id != tenantId)
            {
                throw new AbpAuthorizationException("Merchant access denied.");
            }

            return;
        }

        // Only the accountant/admin (Finance.ReadAll) may read an arbitrary merchant ledger. Previously
        // this allowed Partners.Manage (partner-ops), which leaked every merchant's finance ledger.
        if (await _permissionChecker.IsGrantedAsync(ZahyPermissions.Finance.ReadAll))
        {
            return;
        }

        throw new AbpAuthorizationException("Merchant access denied.");
    }

    public Task<Guid> GetRequiredPartnerIdAsync()
    {
        if (_currentPartner.Id == null)
        {
            throw new AbpAuthorizationException("Partner context is required.");
        }

        return Task.FromResult(_currentPartner.Id.Value);
    }

    public Task<Guid> GetRequiredTenantIdAsync()
    {
        if (_currentTenant.Id == null)
        {
            throw new AbpAuthorizationException("Merchant context is required.");
        }

        return Task.FromResult(_currentTenant.Id.Value);
    }
}
