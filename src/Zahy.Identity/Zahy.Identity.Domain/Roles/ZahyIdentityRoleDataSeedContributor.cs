using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Zahy.Identity.Roles;

namespace Zahy.Identity;

/// <summary>
/// Idempotently seeds the Zahy roles with their least-privilege permission
/// grants. Platform and Partner roles are host-level; Merchant roles are seeded
/// per tenant (when the seeder runs in a tenant context).
/// </summary>
public class ZahyIdentityRoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IdentityRoleManager _roleManager;
    private readonly IPermissionManager _permissionManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public ZahyIdentityRoleDataSeedContributor(
        IIdentityRoleRepository roleRepository,
        IdentityRoleManager roleManager,
        IPermissionManager permissionManager,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _roleRepository = roleRepository;
        _roleManager = roleManager;
        _permissionManager = permissionManager;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (_currentTenant.Id == null)
        {
            await SeedScopeAsync(ZahyRoleScope.Platform);
            await SeedScopeAsync(ZahyRoleScope.Partner);
        }
        else
        {
            await SeedScopeAsync(ZahyRoleScope.Merchant);
        }
    }

    private async Task SeedScopeAsync(ZahyRoleScope scope)
    {
        foreach (var definition in ZahyRoleRegistry.ForScope(scope))
        {
            var role = await _roleRepository.FindByNormalizedNameAsync(
                _roleManager.NormalizeKey(definition.Name));

            if (role == null)
            {
                role = new IdentityRole(_guidGenerator.Create(), definition.Name, _currentTenant.Id)
                {
                    IsStatic = true
                };

                (await _roleManager.CreateAsync(role)).CheckErrors();
            }

            foreach (var permission in definition.Permissions)
            {
                await _permissionManager.SetForRoleAsync(definition.Name, permission, true);
            }
        }
    }
}
