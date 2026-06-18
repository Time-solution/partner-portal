using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Zahy.Identity.Partners;

namespace Zahy.Identity.Roles;

/// <summary>
/// Applies <see cref="ZahyRoleAssignmentPolicy"/> using the current actor's
/// roles/tenant/partner so admins can only grant or revoke roles within their
/// allowed scope and boundary.
/// </summary>
public class ZahyRoleAssignmentManager : DomainService
{
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPartner _currentPartner;
    private readonly IdentityUserManager _userManager;

    public ZahyRoleAssignmentManager(
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        ICurrentPartner currentPartner,
        IdentityUserManager userManager)
    {
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _currentPartner = currentPartner;
        _userManager = userManager;
    }

    public ZahyRoleAssignmentResult Evaluate(IdentityUser targetUser, string roleName)
    {
        return ZahyRoleAssignmentPolicy.Evaluate(
            assignerRoles: _currentUser.Roles,
            assignerTenantId: _currentTenant.Id,
            assignerPartnerId: _currentPartner.Id,
            targetRole: roleName,
            targetTenantId: targetUser.TenantId,
            targetPartnerId: GetPartnerId(targetUser));
    }

    public async Task GrantRoleAsync(IdentityUser targetUser, string roleName)
    {
        EnsureAllowed(targetUser, roleName, "assign");
        (await _userManager.AddToRoleAsync(targetUser, roleName)).CheckErrors();
    }

    public async Task RevokeRoleAsync(IdentityUser targetUser, string roleName)
    {
        EnsureAllowed(targetUser, roleName, "revoke");
        (await _userManager.RemoveFromRoleAsync(targetUser, roleName)).CheckErrors();
    }

    private void EnsureAllowed(IdentityUser targetUser, string roleName, string action)
    {
        var result = Evaluate(targetUser, roleName);
        if (result != ZahyRoleAssignmentResult.Allowed)
        {
            throw new AbpAuthorizationException(
                $"Not allowed to {action} role '{roleName}': {result}.");
        }
    }

    private static Guid? GetPartnerId(IdentityUser user)
    {
        var claim = user.Claims.FirstOrDefault(c => c.ClaimType == ZahyClaimTypes.PartnerId);
        return claim != null && Guid.TryParse(claim.ClaimValue, out var id) ? id : null;
    }
}
