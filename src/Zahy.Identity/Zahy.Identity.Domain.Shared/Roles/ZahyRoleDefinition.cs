using System.Collections.Generic;

namespace Zahy.Identity.Roles;

/// <summary>
/// The static definition of a Zahy role: its scope, the permissions it grants
/// (least-privilege), and the roles it is allowed to assign to others.
/// </summary>
public sealed class ZahyRoleDefinition
{
    public string Name { get; }

    public ZahyRoleScope Scope { get; }

    /// <summary>Permissions granted to this role.</summary>
    public IReadOnlyList<string> Permissions { get; }

    /// <summary>Roles that a holder of this role may assign/revoke (always within boundary).</summary>
    public IReadOnlyList<string> AssignableRoles { get; }

    public ZahyRoleDefinition(
        string name,
        ZahyRoleScope scope,
        IReadOnlyList<string> permissions,
        IReadOnlyList<string> assignableRoles)
    {
        Name = name;
        Scope = scope;
        Permissions = permissions;
        AssignableRoles = assignableRoles;
    }
}
