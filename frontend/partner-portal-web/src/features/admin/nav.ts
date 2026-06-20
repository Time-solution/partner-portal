import {
  Building2,
  FileText,
  KeyRound,
  LayoutDashboard,
  Package,
  RefreshCw,
  RotateCcw,
  ScrollText,
  Settings,
  ShieldCheck,
  ShoppingBag,
  Wallet,
  Webhook,
} from "lucide-react";
import { PortalPermissions } from "@/lib/rbac/portalRoles";

export interface AdminNavItem {
  key: string;
  path: string;
  labelKey: string;
  icon: typeof LayoutDashboard;
  permissions: readonly string[];
}

/** Main admin sections (Step 3 — 9 sections). */
export const adminNav: readonly AdminNavItem[] = [
  {
    key: "dashboard",
    path: "/dashboard",
    labelKey: "navDashboard",
    icon: LayoutDashboard,
    permissions: [PortalPermissions.Dashboard.Read],
  },
  {
    key: "partners",
    path: "/partners",
    labelKey: "navPartners",
    icon: Building2,
    permissions: [PortalPermissions.Partners.Read, PortalPermissions.Partners.Manage],
  },
  {
    key: "catalog",
    path: "/catalog",
    labelKey: "navCatalog",
    icon: Package,
    permissions: [PortalPermissions.Catalog.Read],
  },
  {
    key: "activations",
    path: "/activations",
    labelKey: "navActivations",
    icon: ShoppingBag,
    permissions: [PortalPermissions.Activations.Read],
  },
  {
    key: "settlement",
    path: "/settlement",
    labelKey: "navSettlement",
    icon: Wallet,
    permissions: [PortalPermissions.Settlement.Read],
  },
  {
    key: "reflected-orders",
    path: "/reflected-orders",
    labelKey: "navReflectedOrders",
    icon: RefreshCw,
    permissions: [PortalPermissions.Reflection.Read],
  },
  {
    key: "billing",
    path: "/billing",
    labelKey: "navBilling",
    icon: FileText,
    permissions: [PortalPermissions.Billing.Read],
  },
  {
    key: "reversals",
    path: "/reversals",
    labelKey: "navReversals",
    icon: RotateCcw,
    permissions: [PortalPermissions.Reversals.Read],
  },
  {
    key: "webhooks",
    path: "/webhooks",
    labelKey: "navWebhooksManage",
    icon: Webhook,
    permissions: [PortalPermissions.Webhooks.Read, PortalPermissions.Webhooks.Manage],
  },
  {
    key: "credentials",
    path: "/credentials",
    labelKey: "navCredentials",
    icon: KeyRound,
    permissions: [PortalPermissions.Credentials.Read, PortalPermissions.Credentials.Rotate],
  },
  {
    key: "settings",
    path: "/settings",
    labelKey: "navSettings",
    icon: Settings,
    permissions: [PortalPermissions.Settings.Read],
  },
];

export function filterNavByPermissions(
  items: readonly AdminNavItem[],
  granted: readonly string[],
): AdminNavItem[] {
  return items.filter(
    (item) =>
      item.permissions.length === 0 ||
      item.permissions.some((p) => granted.includes(p) || granted.includes("Zahy.Admin")),
  );
}

/** Legacy export for audit subsection icon reuse. */
export { ScrollText, ShieldCheck };
