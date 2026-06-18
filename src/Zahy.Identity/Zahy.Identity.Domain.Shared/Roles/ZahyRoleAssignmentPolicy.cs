using System;
using System.Collections.Generic;
using System.Linq;

namespace Zahy.Identity.Roles;

public enum ZahyRoleAssignmentResult
{
    Allowed,

    /// <summary>The target role name is not part of the Zahy role model.</summary>
    UnknownRole,

    /// <summary>No role held by the assigner is allowed to assign the target role.</summary>
    NotAssignable,

    /// <summary>The assigner and target are not in the same merchant tenant.</summary>
    TenantBoundary,

    /// <summary>The assigner and target are not in the same partner aggregate.</summary>
    PartnerBoundary
}

/// <summary>
/// Pure (infrastructure-free) decision for "may this actor assign this role to
/// this target?". Enforces least-privilege (assignable roles) plus the tenant
/// and partner boundaries. The domain service wires the current
/// user/tenant/partner into this.
/// </summary>
public static class ZahyRoleAssignmentPolicy
{
    public static ZahyRoleAssignmentResult Evaluate(
        IEnumerable<string> assignerRoles,
        Guid? assignerTenantId,
        Guid? assignerPartnerId,
        string targetRole,
        Guid? targetTenantId,
        Guid? targetPartnerId)
    {
        var target = ZahyRoleRegistry.Find(targetRole);
        if (target == null)
        {
            return ZahyRoleAssignmentResult.UnknownRole;
        }

        var roles = assignerRoles?.ToArray() ?? Array.Empty<string>();

        var authorizingRoles = roles
            .Select(ZahyRoleRegistry.Find)
            .Where(d => d != null && d.AssignableRoles.Contains(target.Name, StringComparer.OrdinalIgnoreCase))
            .Select(d => d!)
            .ToArray();

        if (authorizingRoles.Length == 0)
        {
            return ZahyRoleAssignmentResult.NotAssignable;
        }

        // Platform operators (e.g. SuperAdmin, PartnerOps) are the cross-cutting
        // admins and may assign across tenant/partner boundaries.
        if (authorizingRoles.Any(r => r.Scope == ZahyRoleScope.Platform))
        {
            return ZahyRoleAssignmentResult.Allowed;
        }

        // Otherwise the authority is tenant- or partner-scoped: stay in boundary.
        switch (target.Scope)
        {
            case ZahyRoleScope.Merchant:
                if (assignerTenantId == null || targetTenantId == null ||
                    assignerTenantId != targetTenantId)
                {
                    return ZahyRoleAssignmentResult.TenantBoundary;
                }
                break;

            case ZahyRoleScope.Partner:
                if (assignerPartnerId == null || targetPartnerId == null ||
                    assignerPartnerId != targetPartnerId)
                {
                    return ZahyRoleAssignmentResult.PartnerBoundary;
                }
                break;
        }

        return ZahyRoleAssignmentResult.Allowed;
    }
}
