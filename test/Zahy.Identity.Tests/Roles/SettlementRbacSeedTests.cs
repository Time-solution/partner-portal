using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.PermissionManagement;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;

namespace Zahy.Identity.Roles;

/// <summary>
/// Proves the settlement RBAC contract (DESIGN.md §7): Accountant (PlatformFinance) reads all books
/// and reconciles but cannot disburse; PartnerFinance (PartnerOwner) can only read; only the platform
/// admin can disburse. Data-level scoping (own partner + own book) is enforced in the settlement app
/// services when they are built — this test covers the permission grants.
/// </summary>
public class SettlementRbacSeedTests : ZahyIdentityTestBase
{
    private const string RoleProviderName = "R"; // ABP RolePermissionValueProvider.ProviderName

    private readonly IDataSeeder _dataSeeder;
    private readonly IPermissionManager _permissionManager;

    public SettlementRbacSeedTests()
    {
        _dataSeeder = GetRequiredService<IDataSeeder>();
        _permissionManager = GetRequiredService<IPermissionManager>();
    }

    [Fact]
    public async Task Accountant_Reads_All_Books_And_Reconciles_But_Cannot_Disburse()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync(new DataSeedContext()));

        await WithUnitOfWorkAsync(async () =>
        {
            (await IsGranted(ZahyRoles.PlatformFinance, ZahyPermissions.Settlement.Read)).ShouldBeTrue();
            (await IsGranted(ZahyRoles.PlatformFinance, ZahyPermissions.Settlement.Reconcile)).ShouldBeTrue();
            (await IsGranted(ZahyRoles.PlatformFinance, ZahyPermissions.Settlement.Disburse)).ShouldBeFalse();
        });
    }

    [Fact]
    public async Task PartnerFinance_Can_Read_But_Not_Reconcile_Or_Disburse()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync(new DataSeedContext()));

        await WithUnitOfWorkAsync(async () =>
        {
            (await IsGranted(ZahyRoles.PartnerOwner, ZahyPermissions.Settlement.Read)).ShouldBeTrue();
            (await IsGranted(ZahyRoles.PartnerOwner, ZahyPermissions.Settlement.Reconcile)).ShouldBeFalse();
            (await IsGranted(ZahyRoles.PartnerOwner, ZahyPermissions.Settlement.Disburse)).ShouldBeFalse();
        });
    }

    [Fact]
    public async Task Only_Platform_Admin_Can_Disburse()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync(new DataSeedContext()));

        await WithUnitOfWorkAsync(async () =>
        {
            (await IsGranted(ZahyRoles.PlatformSuperAdmin, ZahyPermissions.Settlement.Disburse)).ShouldBeTrue();
        });
    }

    private async Task<bool> IsGranted(string roleName, string permissionName)
    {
        var result = await _permissionManager.GetAsync(permissionName, RoleProviderName, roleName);
        return result.IsGranted;
    }
}
