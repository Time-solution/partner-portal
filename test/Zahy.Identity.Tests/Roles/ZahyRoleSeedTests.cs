using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;

namespace Zahy.Identity.Roles;

public class ZahyRoleSeedTests : ZahyIdentityTestBase
{
    private readonly IDataSeeder _dataSeeder;
    private readonly IPermissionManager _permissionManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly ICurrentTenant _currentTenant;

    public ZahyRoleSeedTests()
    {
        _dataSeeder = GetRequiredService<IDataSeeder>();
        _permissionManager = GetRequiredService<IPermissionManager>();
        _roleManager = GetRequiredService<IdentityRoleManager>();
        _roleRepository = GetRequiredService<IIdentityRoleRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Should_Seed_Host_Roles_With_LeastPrivilege()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync(new DataSeedContext()));

        await WithUnitOfWorkAsync(async () =>
        {
            (await _roleManager.RoleExistsAsync(ZahyRoles.PlatformSuperAdmin)).ShouldBeTrue();
            (await _roleManager.RoleExistsAsync(ZahyRoles.PartnerOwner)).ShouldBeTrue();

            // SuperAdmin holds everything.
            (await IsGranted(ZahyRoles.PlatformSuperAdmin, ZahyPermissions.Admin)).ShouldBeTrue();
            (await IsGranted(ZahyRoles.PlatformSuperAdmin, ZahyPermissions.Catalog.Write)).ShouldBeTrue();

            // ReadOnly: reads yes, writes no.
            (await IsGranted(ZahyRoles.PlatformReadOnly, ZahyPermissions.Catalog.Read)).ShouldBeTrue();
            (await IsGranted(ZahyRoles.PlatformReadOnly, ZahyPermissions.Catalog.Write)).ShouldBeFalse();

            // PartnerStaff: orders read yes, catalog write no.
            (await IsGranted(ZahyRoles.PartnerStaff, ZahyPermissions.Orders.Read)).ShouldBeTrue();
            (await IsGranted(ZahyRoles.PartnerStaff, ZahyPermissions.Catalog.Write)).ShouldBeFalse();

            // Host context must NOT create tenant-scoped Merchant roles.
            (await _roleManager.RoleExistsAsync(ZahyRoles.MerchantOwner)).ShouldBeFalse();
        });
    }

    [Fact]
    public async Task Host_Role_Seeding_Should_Be_Idempotent()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync(new DataSeedContext()));
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync(new DataSeedContext()));

        await WithUnitOfWorkAsync(async () =>
        {
            var hostRoles = await _roleRepository.GetListAsync();

            var zahyHostRoleNames = ZahyRoleRegistry.ForScope(ZahyRoleScope.Platform)
                .Concat(ZahyRoleRegistry.ForScope(ZahyRoleScope.Partner))
                .Select(d => d.Name)
                .ToList();

            // 5 Platform + 3 Partner Zahy roles, exactly once each (no duplicates).
            hostRoles.Count(r => zahyHostRoleNames.Contains(r.Name)).ShouldBe(8);
        });
    }

    [Fact]
    public async Task Should_Seed_Merchant_Roles_Per_Tenant_And_Isolate_Grants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantA))
            {
                await _dataSeeder.SeedAsync(new DataSeedContext(tenantA));
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantA))
            {
                (await _roleManager.RoleExistsAsync(ZahyRoles.MerchantOwner)).ShouldBeTrue();
                (await IsGranted(ZahyRoles.MerchantOwner, ZahyPermissions.Catalog.Write)).ShouldBeTrue();
                (await IsGranted(ZahyRoles.MerchantOwner, ZahyPermissions.Roles.Manage)).ShouldBeTrue();

                (await IsGranted(ZahyRoles.MerchantViewer, ZahyPermissions.Catalog.Read)).ShouldBeTrue();
                (await IsGranted(ZahyRoles.MerchantViewer, ZahyPermissions.Catalog.Write)).ShouldBeFalse();
                (await IsGranted(ZahyRoles.MerchantViewer, ZahyPermissions.Inventory.Write)).ShouldBeFalse();
            }
        });

        // Tenant B was never seeded: grants must not leak across the tenant boundary.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantB))
            {
                (await IsGranted(ZahyRoles.MerchantOwner, ZahyPermissions.Catalog.Write)).ShouldBeFalse();
            }
        });
    }

    // ABP's RolePermissionValueProvider.ProviderName.
    private const string RoleProviderName = "R";

    private async Task<bool> IsGranted(string roleName, string permissionName)
    {
        var result = await _permissionManager.GetAsync(
            permissionName,
            RoleProviderName,
            roleName);

        return result.IsGranted;
    }
}
