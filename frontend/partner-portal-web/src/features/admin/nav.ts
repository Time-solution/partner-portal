import {
  Building2,
  Calculator,
  KeyRound,
  LayoutDashboard,
  Package,
  Settings,
  ShoppingBag,
  Store,
  Truck,
  UtensilsCrossed,
  Warehouse,
  Webhook,
  Users,
} from "lucide-react";
import { PortalPermissions as PP } from "@/lib/rbac/portalRoles";
import {
  PARTNER_MODULES,
  type PartnerBusinessModuleId,
  canAccessFinanceWorkspace,
  canAccessMerchantPreview,
  canAccessPartnerModules,
} from "@/lib/rbac/partnerModules";
import type { PortalRole } from "@/lib/rbac/portalRoles";

export interface AdminNavItem {
  key: string;
  path: string;
  labelKey: string;
  icon: typeof LayoutDashboard;
  permissions: readonly string[];
  /** Optional role gate beyond permissions */
  roles?: readonly PortalRole[];
  section?: "core" | "modules" | "finance" | "admin";
}

const MODULE_ICONS: Record<PartnerBusinessModuleId, typeof LayoutDashboard> = {
  "delivery-service": Truck,
  commerce: Store,
  fnb: UtensilsCrossed,
  subscriptions: Package,
  marketplace: ShoppingBag,
  consignment: Warehouse,
};

/** First screen path per module for nav links */
const PARTNER_MODULE_FIRST_SCREEN: Record<PartnerBusinessModuleId, string> = {
  "delivery-service": "catalog",
  commerce: "reflected",
  fnb: "reflected",
  subscriptions: "subscriptions",
  marketplace: "catalog",
  consignment: "",
};

const moduleNavItems: AdminNavItem[] = PARTNER_MODULES.map((mod) => ({
  key: mod.id,
  path: mod.comingSoon
    ? `/modules/${mod.id}`
    : `/modules/${mod.id}/${PARTNER_MODULE_FIRST_SCREEN[mod.id]}`,
  labelKey: mod.labelKey,
  icon: MODULE_ICONS[mod.id],
  permissions: mod.permissions,
  roles: ["PlatformAdmin", "PartnerSuccessManager"],
  section: "modules",
}));

export const adminNav: readonly AdminNavItem[] = [
  {
    key: "dashboard",
    path: "/dashboard",
    labelKey: "navDashboard",
    icon: LayoutDashboard,
    permissions: [PP.Dashboard.Read],
    section: "core",
  },
  {
    key: "merchant-preview",
    path: "/merchant-preview",
    labelKey: "navMerchantPreview",
    icon: Users,
    permissions: [PP.MerchantPreview.Read],
    roles: ["PlatformAdmin", "MerchantPreview"],
    section: "core",
  },
  {
    key: "finance",
    path: "/finance/overview",
    labelKey: "navFinance",
    icon: Calculator,
    permissions: [PP.Finance.Read],
    roles: ["Accountant", "PlatformAdmin"],
    section: "finance",
  },
  ...moduleNavItems,
  {
    key: "partners",
    path: "/partners",
    labelKey: "navPartners",
    icon: Building2,
    permissions: [PP.Partners.Read, PP.Partners.Manage],
    section: "core",
  },
  {
    key: "webhooks",
    path: "/webhooks",
    labelKey: "navWebhooksManage",
    icon: Webhook,
    permissions: [PP.Webhooks.Read, PP.Webhooks.Manage],
    section: "admin",
  },
  {
    key: "credentials",
    path: "/credentials",
    labelKey: "navCredentials",
    icon: KeyRound,
    permissions: [PP.Credentials.Read, PP.Credentials.Rotate],
    section: "admin",
  },
  {
    key: "settings",
    path: "/settings",
    labelKey: "navSettings",
    icon: Settings,
    permissions: [PP.Settings.Read],
    section: "admin",
  },
];

export function filterNavByPermissions(
  items: readonly AdminNavItem[],
  granted: readonly string[],
  role?: PortalRole | string,
): AdminNavItem[] {
  return items.filter((item) => {
    if (item.roles?.length && role && !item.roles.includes(role as PortalRole)) {
      return false;
    }
    if (item.section === "modules" && role && !canAccessPartnerModules(role as PortalRole)) {
      return false;
    }
    if (item.key === "finance" && role && !canAccessFinanceWorkspace(role as PortalRole)) {
      return false;
    }
    if (item.key === "merchant-preview" && role && !canAccessMerchantPreview(role as PortalRole)) {
      return false;
    }
    return (
      item.permissions.length === 0 ||
      item.permissions.some((p) => granted.includes(p) || granted.includes("Zahy.Admin"))
    );
  });
}
