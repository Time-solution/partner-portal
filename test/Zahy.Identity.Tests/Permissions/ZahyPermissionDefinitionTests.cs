using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Xunit;
using Zahy.Identity.Permissions;

namespace Zahy.Identity.Permissions;

public class ZahyPermissionDefinitionTests : ZahyIdentityTestBase
{
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;

    public ZahyPermissionDefinitionTests()
    {
        _permissionDefinitionManager = GetRequiredService<IPermissionDefinitionManager>();
    }

    [Theory]
    [InlineData(ZahyPermissions.Catalog.Read)]
    [InlineData(ZahyPermissions.Catalog.Write)]
    [InlineData(ZahyPermissions.Orders.Read)]
    [InlineData(ZahyPermissions.Inventory.Write)]
    [InlineData(ZahyPermissions.Webhooks.Manage)]
    [InlineData(ZahyPermissions.Payouts.Read)]
    [InlineData(ZahyPermissions.Partners.Manage)]
    [InlineData(ZahyPermissions.Roles.Manage)]
    [InlineData(ZahyPermissions.Admin)]
    public async Task Should_Define_All_Zahy_Permissions(string permissionName)
    {
        var definition = await _permissionDefinitionManager.GetOrNullAsync(permissionName);
        definition.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Define_The_Zahy_Group()
    {
        var groups = await _permissionDefinitionManager.GetGroupsAsync();
        groups.Any(g => g.Name == ZahyPermissions.GroupName).ShouldBeTrue();
    }
}
