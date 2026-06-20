using System;
using System.Collections.Generic;
using System.Linq;
using Zahy.Identity.Permissions;

namespace Zahy.Identity.Roles;

/// <summary>
/// The single source of truth for the Zahy role model: scope, least-privilege
/// permission grants, and assignment authority. Both the data seeder and the
/// role-assignment policy read from here.
/// </summary>
public static class ZahyRoleRegistry
{
    private static readonly ZahyRoleDefinition[] Definitions =
    {
        // ----- Platform (host) -------------------------------------------------
        new(
            ZahyRoles.PlatformSuperAdmin,
            ZahyRoleScope.Platform,
            permissions: ZahyPermissions.All(),
            assignableRoles: AllRoleNames()),

        new(
            ZahyRoles.PlatformPartnerOps,
            ZahyRoleScope.Platform,
            permissions: new[]
            {
                ZahyPermissions.Partners.Manage,
                ZahyPermissions.Catalog.Read,
                ZahyPermissions.Orders.Read,
                ZahyPermissions.Roles.Manage
            },
            assignableRoles: new[]
            {
                ZahyRoles.PartnerOwner,
                ZahyRoles.PartnerManager,
                ZahyRoles.PartnerStaff
            }),

        new(
            ZahyRoles.PlatformFinance,
            ZahyRoleScope.Platform,
            permissions: new[]
            {
                ZahyPermissions.Payouts.Read,
                ZahyPermissions.Orders.Read,
                ZahyPermissions.Finance.KycReview,
                ZahyPermissions.Settlement.Read,
                ZahyPermissions.Settlement.Reconcile
            },
            assignableRoles: Array.Empty<string>()),

        new(
            ZahyRoles.PlatformSupport,
            ZahyRoleScope.Platform,
            permissions: new[]
            {
                ZahyPermissions.Orders.Read,
                ZahyPermissions.Catalog.Read
            },
            assignableRoles: Array.Empty<string>()),

        new(
            ZahyRoles.PlatformReadOnly,
            ZahyRoleScope.Platform,
            permissions: new[]
            {
                ZahyPermissions.Catalog.Read,
                ZahyPermissions.Orders.Read,
                ZahyPermissions.Payouts.Read
            },
            assignableRoles: Array.Empty<string>()),

        // ----- Merchant (tenant) ----------------------------------------------
        new(
            ZahyRoles.MerchantOwner,
            ZahyRoleScope.Merchant,
            permissions: new[]
            {
                ZahyPermissions.Catalog.Read,
                ZahyPermissions.Catalog.Write,
                ZahyPermissions.Orders.Read,
                ZahyPermissions.Inventory.Write,
                ZahyPermissions.Payouts.Read,
                ZahyPermissions.Roles.Manage
            },
            assignableRoles: new[]
            {
                ZahyRoles.MerchantManager,
                ZahyRoles.MerchantStaff,
                ZahyRoles.MerchantViewer
            }),

        new(
            ZahyRoles.MerchantManager,
            ZahyRoleScope.Merchant,
            permissions: new[]
            {
                ZahyPermissions.Catalog.Read,
                ZahyPermissions.Catalog.Write,
                ZahyPermissions.Orders.Read,
                ZahyPermissions.Inventory.Write
            },
            assignableRoles: new[]
            {
                ZahyRoles.MerchantStaff,
                ZahyRoles.MerchantViewer
            }),

        new(
            ZahyRoles.MerchantStaff,
            ZahyRoleScope.Merchant,
            permissions: new[]
            {
                ZahyPermissions.Catalog.Read,
                ZahyPermissions.Orders.Read,
                ZahyPermissions.Inventory.Write
            },
            assignableRoles: Array.Empty<string>()),

        new(
            ZahyRoles.MerchantViewer,
            ZahyRoleScope.Merchant,
            permissions: new[]
            {
                ZahyPermissions.Catalog.Read,
                ZahyPermissions.Orders.Read
            },
            assignableRoles: Array.Empty<string>()),

        // ----- Partner (host aggregate) ---------------------------------------
        new(
            ZahyRoles.PartnerOwner,
            ZahyRoleScope.Partner,
            permissions: new[]
            {
                ZahyPermissions.Catalog.Read,
                ZahyPermissions.Catalog.Write,
                ZahyPermissions.Orders.Read,
                ZahyPermissions.Inventory.Write,
                ZahyPermissions.Webhooks.Manage,
                ZahyPermissions.Payouts.Read,
                ZahyPermissions.Roles.Manage,
                ZahyPermissions.Settlement.Read
            },
            assignableRoles: new[]
            {
                ZahyRoles.PartnerManager,
                ZahyRoles.PartnerStaff
            }),

        new(
            ZahyRoles.PartnerManager,
            ZahyRoleScope.Partner,
            permissions: new[]
            {
                ZahyPermissions.Catalog.Read,
                ZahyPermissions.Catalog.Write,
                ZahyPermissions.Orders.Read,
                ZahyPermissions.Inventory.Write,
                ZahyPermissions.Webhooks.Manage,
                ZahyPermissions.Settlement.Read
            },
            assignableRoles: new[]
            {
                ZahyRoles.PartnerStaff
            }),

        new(
            ZahyRoles.PartnerStaff,
            ZahyRoleScope.Partner,
            permissions: new[]
            {
                ZahyPermissions.Catalog.Read,
                ZahyPermissions.Orders.Read
            },
            assignableRoles: Array.Empty<string>())
    };

    public static IReadOnlyList<ZahyRoleDefinition> All => Definitions;

    public static IEnumerable<ZahyRoleDefinition> ForScope(ZahyRoleScope scope)
    {
        return Definitions.Where(d => d.Scope == scope);
    }

    public static ZahyRoleDefinition? Find(string roleName)
    {
        return Definitions.FirstOrDefault(d =>
            string.Equals(d.Name, roleName, StringComparison.OrdinalIgnoreCase));
    }

    public static string[] AllRoleNames()
    {
        return new[]
        {
            ZahyRoles.PlatformSuperAdmin,
            ZahyRoles.PlatformPartnerOps,
            ZahyRoles.PlatformFinance,
            ZahyRoles.PlatformSupport,
            ZahyRoles.PlatformReadOnly,
            ZahyRoles.MerchantOwner,
            ZahyRoles.MerchantManager,
            ZahyRoles.MerchantStaff,
            ZahyRoles.MerchantViewer,
            ZahyRoles.PartnerOwner,
            ZahyRoles.PartnerManager,
            ZahyRoles.PartnerStaff
        };
    }
}
