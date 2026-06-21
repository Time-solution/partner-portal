using System.Linq;
using Shouldly;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;

namespace Zahy.Settlement;

/// <summary>
/// Separation of duties for disburse/release (uses the EXISTING role model — no new roles). Disburse
/// is a SEPARATE, higher privilege than Reconcile: the accountant (Platform.Finance) can reconcile but
/// must NOT disburse/release; the platform admin (Platform.SuperAdmin) holds Disburse; partner roles
/// hold neither.
/// </summary>
public class DisbursementRbacTests
{
    private static string[] PermissionsOf(string role) =>
        ZahyRoleRegistry.Find(role).ShouldNotBeNull().Permissions.ToArray();

    [Fact]
    public void Accountant_Can_Reconcile_But_Cannot_Disburse()
    {
        var finance = PermissionsOf(ZahyRoles.PlatformFinance);

        finance.ShouldContain(ZahyPermissions.Settlement.Reconcile);
        finance.ShouldNotContain(ZahyPermissions.Settlement.Disburse);
    }

    [Fact]
    public void Platform_Admin_Can_Disburse()
    {
        PermissionsOf(ZahyRoles.PlatformSuperAdmin).ShouldContain(ZahyPermissions.Settlement.Disburse);
    }

    [Fact]
    public void Partner_Roles_Cannot_Disburse()
    {
        foreach (var role in new[] { ZahyRoles.PartnerOwner, ZahyRoles.PartnerManager, ZahyRoles.PartnerStaff })
        {
            PermissionsOf(role).ShouldNotContain(ZahyPermissions.Settlement.Disburse);
        }
    }

    [Fact]
    public void Disburse_Is_A_Separate_Privilege_From_Reconcile()
    {
        ZahyPermissions.Settlement.Disburse.ShouldNotBe(ZahyPermissions.Settlement.Reconcile);
    }
}
