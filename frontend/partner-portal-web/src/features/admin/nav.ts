import {
  Boxes,
  Building2,
  LayoutDashboard,
  Package,
  ScrollText,
  ShieldCheck,
  ShoppingCart,
  Wallet,
  Webhook,
  type LucideIcon,
} from "lucide-react";
import { hasAnyPermission, Permissions } from "@/lib/permissions";

export interface AdminNavItem {
  key: string;
  labelKey: string;
  icon: LucideIcon;
  /** Required permissions (any-of). Empty means visible to any signed-in user. */
  permissions: readonly string[];
}

export const adminNav: readonly AdminNavItem[] = [
  { key: "dashboard", labelKey: "navDashboard", icon: LayoutDashboard, permissions: [] },
  {
    key: "catalog",
    labelKey: "navCatalog",
    icon: Package,
    permissions: [Permissions.Catalog.Read, Permissions.Catalog.Write],
  },
  { key: "orders", labelKey: "navOrders", icon: ShoppingCart, permissions: [Permissions.Orders.Read] },
  { key: "inventory", labelKey: "navInventory", icon: Boxes, permissions: [Permissions.Inventory.Write] },
  { key: "webhooks", labelKey: "navWebhooks", icon: Webhook, permissions: [Permissions.Webhooks.Manage] },
  { key: "payouts", labelKey: "navPayouts", icon: Wallet, permissions: [Permissions.Payouts.Read] },
  { key: "partners", labelKey: "navPartners", icon: Building2, permissions: [Permissions.Partners.Manage] },
  { key: "roles", labelKey: "navRoles", icon: ShieldCheck, permissions: [Permissions.Roles.Manage] },
  { key: "audit", labelKey: "navAudit", icon: ScrollText, permissions: [Permissions.Admin] },
];

/** Pure role-gating: returns only the nav items the user is permitted to see. */
export function filterNavByPermissions(
  items: readonly AdminNavItem[],
  granted: readonly string[],
): AdminNavItem[] {
  return items.filter(
    (item) => item.permissions.length === 0 || hasAnyPermission(granted, item.permissions),
  );
}
