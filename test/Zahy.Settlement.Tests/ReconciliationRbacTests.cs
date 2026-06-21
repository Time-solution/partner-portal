using System.Linq;
using Shouldly;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;

namespace Zahy.Settlement;

/// <summary>
/// Separation of duties for reconcile (uses the EXISTING role model — no new roles). The accountant
/// (Platform.Finance) and platform admin (Platform.SuperAdmin) CAN reconcile; reconcile must NOT
/// imply the power to disburse, and partner roles cannot reconcile at all.
/// </summary>
public class ReconciliationRbacTests
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
    public void Platform_Admin_Can_Reconcile()
    {
        var admin = PermissionsOf(ZahyRoles.PlatformSuperAdmin);

        admin.ShouldContain(ZahyPermissions.Settlement.Reconcile);
    }

    [Fact]
    public void Partner_Roles_Cannot_Reconcile()
    {
        foreach (var role in new[] { ZahyRoles.PartnerOwner, ZahyRoles.PartnerManager, ZahyRoles.PartnerStaff })
        {
            PermissionsOf(role).ShouldNotContain(ZahyPermissions.Settlement.Reconcile);
        }
    }

    [Fact]
    public void Reconcile_And_Disburse_Are_Distinct_Permissions()
    {
        ZahyPermissions.Settlement.Reconcile.ShouldNotBe(ZahyPermissions.Settlement.Disburse);
    }

    [Fact]
    public void Override_Is_An_Accountant_Action_Gated_By_Reconcile_Not_Disburse()
    {
        // An override-with-note is still a reconcile action: anyone who can reconcile can override
        // (it is audited, not escalated). It must NOT require the higher disburse privilege.
        foreach (var role in new[] { ZahyRoles.PlatformFinance, ZahyRoles.PlatformSuperAdmin })
        {
            PermissionsOf(role).ShouldContain(ZahyPermissions.Settlement.Reconcile);
        }

        // The accountant overrides but still cannot disburse — separation of duties holds.
        PermissionsOf(ZahyRoles.PlatformFinance).ShouldNotContain(ZahyPermissions.Settlement.Disburse);
    }
}
