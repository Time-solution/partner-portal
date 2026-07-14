import {
  Boxes,
  Building2,
  Calculator,
  CheckCircle2,
  ClipboardList,
  FileBarChart,
  FilePlus,
  FileText,
  Home,
  KeyRound,
  LayoutDashboard,
  Package,
  Receipt,
  Settings,
  ShoppingBag,
  Sparkles,
  Store,
  Truck,
  UserRound,
  UtensilsCrossed,
  Users,
  Warehouse,
  Webhook,
  Zap,
} from "lucide-react";
import { PortalPermissions as PP } from "@/lib/rbac/portalRoles";
import {
  PARTNER_MODULES,
  type PartnerBusinessModuleId,
  canAccessFinanceWorkspace,
  canAccessMerchantPreview,
} from "@/lib/rbac/partnerModules";
import type { PortalRole } from "@/lib/rbac/portalRoles";
import { ROLE_NAV_KEYS, partnerHomePath } from "@/lib/rbac/roleNavConfig";

export interface AdminNavItem {
  key: string;
  path: string;
  labelKey: string;
  icon: typeof LayoutDashboard;
  permissions: readonly string[];
  /** Optional role gate beyond permissions */
  roles?: readonly PortalRole[];
  section?: "core" | "modules" | "finance" | "admin" | "partner";
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
  roles: ["PlatformAdmin"],
  section: "modules",
}));

export const adminNav: readonly AdminNavItem[] = [
  {
    key: "dashboard",
    path: "/dashboard",
    labelKey: "navDashboard",
    icon: LayoutDashboard,
    permissions: [PP.Dashboard.Read],
    roles: ["PlatformAdmin", "Accountant"],
    section: "core",
  },
  {
    key: "partner-home",
    path: "/partner",
    labelKey: "navPartnerHome",
    icon: Home,
    permissions: [PP.Partners.Read, PP.PartnerFinance.ReadOwn],
    roles: ["PartnerSuccessManager", "PartnerFinance"],
    section: "partner",
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
  // ---- Merchant sidebar (MerchantPreview role) — tabs promoted to nav entries ----
  {
    key: "merchant-dashboard",
    path: "/merchant-preview",
    labelKey: "navMerchantDashboard",
    icon: LayoutDashboard,
    permissions: [PP.MerchantPreview.Read],
    roles: ["MerchantPreview"],
    section: "core",
  },
  {
    key: "merchant-browse",
    path: "/merchant-preview?tab=browse",
    labelKey: "navMerchantBrowse",
    icon: ShoppingBag,
    permissions: [PP.MerchantPreview.Read],
    roles: ["MerchantPreview"],
    section: "core",
  },
  {
    key: "merchant-services",
    path: "/merchant-preview?tab=services",
    labelKey: "navMerchantServices",
    icon: Sparkles,
    permissions: [PP.MerchantPreview.Read],
    roles: ["MerchantPreview"],
    section: "core",
  },
  {
    key: "merchant-orders",
    path: "/merchant-preview?tab=orders",
    labelKey: "navMerchantOrders",
    icon: ClipboardList,
    permissions: [PP.MerchantPreview.Read],
    roles: ["MerchantPreview"],
    section: "core",
  },
  {
    key: "merchant-invoices",
    path: "/merchant-preview?tab=invoices",
    labelKey: "navMerchantInvoices",
    icon: Receipt,
    permissions: [PP.MerchantPreview.Read],
    roles: ["MerchantPreview"],
    section: "core",
  },
  {
    key: "merchant-statement",
    path: "/merchant-preview?tab=statement",
    labelKey: "navMerchantStatement",
    icon: FileBarChart,
    permissions: [PP.MerchantPreview.Read],
    roles: ["MerchantPreview"],
    section: "core",
  },
  {
    key: "merchant-profile",
    path: "/merchant-preview?tab=profile",
    labelKey: "navMerchantProfile",
    icon: UserRound,
    permissions: [PP.MerchantPreview.Read],
    roles: ["MerchantPreview"],
    section: "core",
  },
  // ---- Partner sidebar (PSM / PartnerFinance) — direct entries; paths bound to own partner id ----
  {
    key: "partner-catalog",
    path: "/partner",
    labelKey: "navPartnerCatalog",
    icon: Package,
    permissions: [PP.Catalog.Read],
    roles: ["PartnerSuccessManager", "PartnerFinance"],
    section: "partner",
  },
  {
    key: "partner-packages",
    path: "/partner",
    labelKey: "navPartnerPackages",
    icon: Boxes,
    permissions: [PP.Catalog.Read],
    roles: ["PartnerSuccessManager", "PartnerFinance"],
    section: "partner",
  },
  {
    key: "partner-activations",
    path: "/partner",
    labelKey: "navPartnerActivations",
    icon: Zap,
    permissions: [PP.Activations.Read],
    roles: ["PartnerSuccessManager", "PartnerFinance"],
    section: "partner",
  },
  {
    key: "partner-orders",
    path: "/partner",
    labelKey: "navPartnerOrders",
    icon: ClipboardList,
    permissions: [PP.Catalog.Read],
    roles: ["PartnerSuccessManager", "PartnerFinance"],
    section: "partner",
  },
  {
    key: "partner-statements",
    path: "/partner",
    labelKey: "navPartnerStatements",
    icon: FileText,
    permissions: [PP.Settlement.Read],
    roles: ["PartnerSuccessManager", "PartnerFinance"],
    section: "partner",
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
  {
    // Deep-link into the SAME Reports view inside /finance/* (not a second page).
    key: "reports",
    path: "/finance/reports",
    labelKey: "navReports",
    icon: FileBarChart,
    permissions: [PP.Finance.Read],
    roles: ["Accountant", "PlatformAdmin"],
    section: "finance",
  },
  {
    key: "commission-approvals",
    path: "/finance/commission-approvals",
    labelKey: "navCommissionApprovals",
    icon: CheckCircle2,
    permissions: [PP.Commission.Approve],
    roles: ["Accountant", "PlatformAdmin"],
    section: "finance",
  },
  {
    key: "manual-invoice-new",
    path: "/finance/invoices/new",
    labelKey: "navManualInvoiceNew",
    icon: FilePlus,
    permissions: [PP.Finance.WriteManualInvoice],
    roles: ["PlatformAdmin", "Accountant"],
    section: "finance",
  },
  ...moduleNavItems,
  {
    key: "partners",
    path: "/partners",
    labelKey: "navPartners",
    icon: Building2,
    permissions: [PP.Partners.Read, PP.Partners.Manage],
    roles: ["PlatformAdmin"],
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
    // Last partner entry by design (matches the ratified sidebar order: … Webhooks / Credentials / الإعدادات).
    key: "partner-settings",
    path: "/partner",
    labelKey: "navPartnerSettings",
    icon: Settings,
    permissions: [PP.Partners.Read, PP.PartnerFinance.ReadOwn],
    roles: ["PartnerSuccessManager", "PartnerFinance"],
    section: "partner",
  },
  {
    key: "settings",
    path: "/settings",
    labelKey: "navSettings",
    icon: Settings,
    permissions: [PP.Settings.Read],
    roles: ["PlatformAdmin", "Accountant"],
    section: "admin",
  },
];

export function filterNavByPermissions(
  items: readonly AdminNavItem[],
  granted: readonly string[],
  role?: PortalRole | string,
): AdminNavItem[] {
  const portalRole = role as PortalRole | undefined;
  const allowedKeys = portalRole ? ROLE_NAV_KEYS[portalRole] : undefined;

  return items.filter((item) => {
    if (allowedKeys && !allowedKeys.includes(item.key)) {
      return false;
    }

    if (item.roles?.length && role && !item.roles.includes(role as PortalRole)) {
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

/** Partner sidebar entries → the partner-scoped screen each one opens (all under /partners/{ownId}). */
const PARTNER_NAV_PATHS: Record<string, (partnerId: string) => string> = {
  "partner-home": partnerHomePath,
  "partner-catalog": (id) => `/partners/${id}/catalog`,
  "partner-packages": (id) => `/partners/${id}/usage-packages`,
  "partner-activations": (id) => `/partners/${id}/activations`,
  "partner-orders": (id) => `/partners/${id}/orders`,
  "partner-statements": (id) => `/partners/${id}/statement`,
  "partner-settings": (id) => `/partners/${id}/profile`,
};

/** Resolve nav for the signed-in role — partner entry paths bind to the scoped partner id. */
export function navForRole(
  role: PortalRole,
  granted: readonly string[],
  scopedPartnerId?: string,
): AdminNavItem[] {
  return filterNavByPermissions(adminNav, granted, role).map((item) => {
    const partnerPath = PARTNER_NAV_PATHS[item.key];
    if (partnerPath && scopedPartnerId) {
      return { ...item, path: partnerPath(scopedPartnerId) };
    }
    return item;
  });
}
