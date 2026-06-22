using System.Linq;
using Shouldly;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;

namespace Zahy.Settlement;

/// <summary>
/// Track B — only the accountant (Platform.Finance) and platform admin (Platform.SuperAdmin) may manage
/// the bank registry. Partner and merchant roles can NEVER manage banks. Uses the EXISTING role model.
/// </summary>
public class BankRegistryRbacTests
{
    private static string[] PermissionsOf(string role) =>
        ZahyRoleRegistry.Find(role).ShouldNotBeNull().Permissions.ToArray();

    [Fact]
    public void Accountant_Can_Manage_The_Bank_Registry()
    {
        PermissionsOf(ZahyRoles.PlatformFinance).ShouldContain(ZahyPermissions.Settlement.BankRegistryManage);
    }

    [Fact]
    public void Platform_Admin_Can_Manage_The_Bank_Registry()
    {
        PermissionsOf(ZahyRoles.PlatformSuperAdmin).ShouldContain(ZahyPermissions.Settlement.BankRegistryManage);
    }

    [Fact]
    public void Partner_Roles_Cannot_Manage_Banks()
    {
        foreach (var role in new[] { ZahyRoles.PartnerOwner, ZahyRoles.PartnerManager, ZahyRoles.PartnerStaff })
        {
            PermissionsOf(role).ShouldNotContain(ZahyPermissions.Settlement.BankRegistryManage);
        }
    }

    [Fact]
    public void Merchant_Roles_Cannot_Manage_Banks()
    {
        foreach (var role in new[]
        {
            ZahyRoles.MerchantOwner, ZahyRoles.MerchantManager, ZahyRoles.MerchantStaff, ZahyRoles.MerchantViewer
        })
        {
            PermissionsOf(role).ShouldNotContain(ZahyPermissions.Settlement.BankRegistryManage);
        }
    }

    [Fact]
    public void Bank_Registry_Is_A_Distinct_Permission_From_Disburse()
    {
        ZahyPermissions.Settlement.BankRegistryManage.ShouldNotBe(ZahyPermissions.Settlement.Disburse);
        // Accountant manages banks but still cannot disburse — separation of duties holds.
        PermissionsOf(ZahyRoles.PlatformFinance).ShouldNotContain(ZahyPermissions.Settlement.Disburse);
    }
}
