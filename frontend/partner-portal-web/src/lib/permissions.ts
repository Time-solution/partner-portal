// Frontend mirror of backend Zahy.Identity.Permissions.ZahyPermissions.
// Kept in sync manually; the backend remains the source of truth and enforces
// every check server-side. These are used only to gate UI affordances.

export const ADMIN_PERMISSION = "Zahy.Admin";

export const Permissions = {
  Catalog: {
    Read: "Zahy.Catalog.Read",
    Write: "Zahy.Catalog.Write",
  },
  Orders: {
    Read: "Zahy.Orders.Read",
  },
  Inventory: {
    Write: "Zahy.Inventory.Write",
  },
  Webhooks: {
    Manage: "Zahy.Webhooks.Manage",
  },
  Payouts: {
    Read: "Zahy.Payouts.Read",
  },
  Partners: {
    Manage: "Zahy.Partners.Manage",
  },
  Roles: {
    Manage: "Zahy.Roles.Manage",
  },
  Admin: ADMIN_PERMISSION,
} as const;

/** Platform Admin is a super-power that implies every other permission. */
export function hasPermission(granted: readonly string[], required: string): boolean {
  return granted.includes(ADMIN_PERMISSION) || granted.includes(required);
}

export function hasAnyPermission(
  granted: readonly string[],
  required: readonly string[],
): boolean {
  if (required.length === 0) return true;
  if (granted.includes(ADMIN_PERMISSION)) return true;
  return required.some((permission) => granted.includes(permission));
}
