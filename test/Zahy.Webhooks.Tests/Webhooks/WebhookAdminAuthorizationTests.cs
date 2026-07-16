using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;

namespace Zahy.Webhooks;

/// <summary>
/// AF1 — the cross-partner webhook admin surface must be gated on the platform-admin permission, NOT
/// Webhooks.Manage (which partner roles hold for their OWN filtered self-service). This closes the
/// audit 🔴: WebhookAdminAppService disables the partner data filter and accepts a caller-supplied
/// partnerId, so any holder reads every partner's data + can replay any dead letter.
/// Deterministic reflection + role-registry assertions (the test host uses AlwaysAllowAuthorization,
/// so we verify the DECLARED gate and the registry grants rather than a live permission check).
/// </summary>
public class WebhookAdminAuthorizationTests
{
    private static string ClassPermission(System.Type type) =>
        type.GetCustomAttribute<AuthorizeAttribute>()!.Policy!;

    [Fact]
    public void Admin_Surface_Is_Gated_On_Platform_Admin_Not_Webhooks_Manage()
    {
        var gate = ClassPermission(typeof(WebhookAdminAppService));
        gate.ShouldBe(ZahyPermissions.Admin);
        gate.ShouldNotBe(ZahyPermissions.Webhooks.Manage);
    }

    [Fact]
    public void Every_Admin_Method_Is_Covered_By_The_Class_Gate_And_None_Downgrades_It()
    {
        // No public admin method may carry a weaker method-level [Authorize] that reopens the surface.
        foreach (var method in typeof(WebhookAdminAppService)
                     .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var methodAttr = method.GetCustomAttribute<AuthorizeAttribute>();
            if (methodAttr?.Policy != null)
            {
                methodAttr.Policy.ShouldBe(ZahyPermissions.Admin, $"{method.Name} must not downgrade the gate");
            }
        }
    }

    [Fact]
    public void Partner_Self_Service_Surface_Keeps_Webhooks_Manage()
    {
        // The partner-filtered path stays on Webhooks.Manage so partner self-service is unaffected.
        ClassPermission(typeof(WebhookSubscriptionAppService)).ShouldBe(ZahyPermissions.Webhooks.Manage);
    }

    [Fact]
    public void Partner_Roles_Hold_Webhooks_Manage_But_NOT_Admin()
    {
        foreach (var roleName in new[] { ZahyRoles.PartnerOwner, ZahyRoles.PartnerManager })
        {
            var role = ZahyRoleRegistry.Find(roleName)!;
            role.Permissions.ShouldContain(ZahyPermissions.Webhooks.Manage, $"{roleName} keeps self-service");
            role.Permissions.ShouldNotContain(ZahyPermissions.Admin,
                $"{roleName} must NOT reach the cross-partner admin surface");
        }
    }

    [Fact]
    public void Only_Platform_Finance_And_SuperAdmin_Hold_Admin()
    {
        foreach (var role in ZahyRoleRegistry.All)
        {
            var holdsAdmin = role.Permissions.Contains(ZahyPermissions.Admin) ||
                             role.Name == ZahyRoles.PlatformSuperAdmin; // SuperAdmin = All()
            if (holdsAdmin)
            {
                new[] { ZahyRoles.PlatformFinance, ZahyRoles.PlatformSuperAdmin }
                    .ShouldContain(role.Name, $"{role.Name} unexpectedly holds Admin");
            }
        }
    }
}
