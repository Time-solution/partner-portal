using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;
using Zahy.Settlement.AggregatorReconciliation;

namespace Zahy.Settlement;

/// <summary>
/// Reuses the EXISTING permission model (no new nodes): view = Settlement.Read, every reconcile
/// command = Settlement.Reconcile. Accountant + admin hold them; partner/merchant roles hold
/// neither — v1 exposes nothing of this area to them. The two-person split is enforced in the
/// DOMAIN (AggregatorStatement.Close), not by role wiring, mirroring reconciler ≠ releaser.
/// </summary>
public class AggregatorReconciliationRbacTests
{
    private static string[] PermissionsOf(string role) =>
        ZahyRoleRegistry.Find(role).ShouldNotBeNull().Permissions.ToArray();

    [Fact]
    public void Accountant_And_Admin_Can_View_And_Reconcile()
    {
        foreach (var role in new[] { ZahyRoles.PlatformFinance, ZahyRoles.PlatformSuperAdmin })
        {
            var permissions = PermissionsOf(role);
            permissions.ShouldContain(ZahyPermissions.Settlement.Read);
            permissions.ShouldContain(ZahyPermissions.Settlement.Reconcile);
        }
    }

    [Fact]
    public void Partner_Roles_Hold_Neither_View_Nor_Reconcile()
    {
        foreach (var role in new[] { ZahyRoles.PartnerOwner, ZahyRoles.PartnerManager, ZahyRoles.PartnerStaff })
        {
            var permissions = PermissionsOf(role);
            permissions.ShouldNotContain(ZahyPermissions.Settlement.Reconcile);
        }
    }

    [Theory]
    [InlineData(nameof(AggregatorReconciliationAppService.ImportAsync))]
    [InlineData(nameof(AggregatorReconciliationAppService.MatchAsync))]
    [InlineData(nameof(AggregatorReconciliationAppService.ResolveExceptionAsync))]
    [InlineData(nameof(AggregatorReconciliationAppService.CloseAsync))]
    public void Every_Command_Is_Gated_By_The_Reconcile_Permission(string methodName)
    {
        AuthorizePolicyOf(methodName).ShouldBe(ZahyPermissions.Settlement.Reconcile);
    }

    [Theory]
    [InlineData(nameof(AggregatorReconciliationAppService.GetListAsync))]
    [InlineData(nameof(AggregatorReconciliationAppService.GetAsync))]
    public void Every_Query_Is_Gated_By_The_Read_Permission(string methodName)
    {
        AuthorizePolicyOf(methodName).ShouldBe(ZahyPermissions.Settlement.Read);
    }

    private static string AuthorizePolicyOf(string methodName)
    {
        var method = typeof(AggregatorReconciliationAppService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == methodName && m.DeclaringType == typeof(AggregatorReconciliationAppService));

        return method.GetCustomAttribute<AuthorizeAttribute>().ShouldNotBeNull().Policy.ShouldNotBeNull();
    }
}
