import type { Partner } from "@/lib/data/types";
import { PortalPermissions as PP } from "./portalRoles";
import type { PortalRole } from "./portalRoles";

/** Six partner business modules — navigation/display grouping only. */
export type PartnerBusinessModuleId =
  | "delivery-service"
  | "commerce"
  | "fnb"
  | "subscriptions"
  | "marketplace"
  | "consignment";

/** Screen ids — each maps to one built page component (single source of truth). */
export type ModuleScreenId =
  | "catalog"
  | "pricing"
  | "settlement"
  | "reversals"
  | "reflected"
  | "pos-sync"
  | "menu"
  | "subscriptions"
  | "billing"
  | "invoices"
  | "activations"
  | "snapshots"
  | "usage-packages";

export interface ModuleScreenDef {
  id: ModuleScreenId;
  path: string;
  labelKey: string;
  permissions: readonly string[];
}

export interface PartnerModuleDef {
  id: PartnerBusinessModuleId;
  labelKey: string;
  descKey: string;
  permissions: readonly string[];
  comingSoon?: boolean;
}

/**
 * Which screens each business module renders.
 * Screens are built once; modules declare visibility here — not separate portals.
 */
export const PARTNER_MODULE_SCREENS: Record<PartnerBusinessModuleId, ModuleScreenDef[]> = {
  "delivery-service": [
    { id: "catalog", path: "catalog", labelKey: "moduleScreen_catalog", permissions: [PP.Catalog.Read] },
    { id: "pricing", path: "pricing", labelKey: "moduleScreen_pricing", permissions: [PP.Catalog.Read] },
    {
      id: "settlement",
      path: "settlement",
      labelKey: "moduleScreen_settlement",
      permissions: [PP.Settlement.Read],
    },
    { id: "reversals", path: "reversals", labelKey: "moduleScreen_reversals", permissions: [PP.Reversals.Read] },
  ],
  commerce: [
    { id: "reflected", path: "reflected", labelKey: "moduleScreen_reflected", permissions: [PP.Reflection.Read] },
    { id: "pos-sync", path: "pos-sync", labelKey: "moduleScreen_posSync", permissions: [PP.Reflection.Read] },
  ],
  fnb: [
    { id: "reflected", path: "reflected", labelKey: "moduleScreen_reflected", permissions: [PP.Reflection.Read] },
    { id: "pos-sync", path: "pos-sync", labelKey: "moduleScreen_posSync", permissions: [PP.Reflection.Read] },
    { id: "menu", path: "menu", labelKey: "moduleScreen_menu", permissions: [PP.Catalog.Read] },
  ],
  subscriptions: [
    {
      id: "subscriptions",
      path: "subscriptions",
      labelKey: "moduleScreen_subscriptions",
      permissions: [PP.Billing.Read],
    },
    {
      id: "usage-packages",
      path: "usage-packages",
      labelKey: "moduleScreen_usagePackages",
      permissions: [PP.Catalog.Read],
    },
    { id: "billing", path: "billing", labelKey: "moduleScreen_billing", permissions: [PP.Billing.Read] },
    { id: "invoices", path: "invoices", labelKey: "moduleScreen_invoices", permissions: [PP.Billing.Read] },
  ],
  marketplace: [
    { id: "catalog", path: "catalog", labelKey: "moduleScreen_catalog", permissions: [PP.Catalog.Read] },
    {
      id: "activations",
      path: "activations",
      labelKey: "moduleScreen_activations",
      permissions: [PP.Activations.Read],
    },
    { id: "snapshots", path: "snapshots", labelKey: "moduleScreen_snapshots", permissions: [PP.Catalog.Read] },
    {
      id: "settlement",
      path: "settlement",
      labelKey: "moduleScreen_settlement",
      permissions: [PP.Settlement.Read],
    },
  ],
  consignment: [],
};

export const PARTNER_MODULES: readonly PartnerModuleDef[] = [
  {
    id: "delivery-service",
    labelKey: "module_deliveryService",
    descKey: "module_deliveryServiceDesc",
    permissions: [PP.Catalog.Read, PP.Settlement.Read, PP.Reversals.Read],
  },
  {
    id: "commerce",
    labelKey: "module_commerce",
    descKey: "module_commerceDesc",
    permissions: [PP.Reflection.Read],
  },
  {
    id: "fnb",
    labelKey: "module_fnb",
    descKey: "module_fnbDesc",
    permissions: [PP.Reflection.Read, PP.Catalog.Read],
  },
  {
    id: "subscriptions",
    labelKey: "module_subscriptions",
    descKey: "module_subscriptionsDesc",
    permissions: [PP.Billing.Read],
  },
  {
    id: "marketplace",
    labelKey: "module_marketplace",
    descKey: "module_marketplaceDesc",
    permissions: [PP.Catalog.Read, PP.Activations.Read, PP.Settlement.Read],
  },
  {
    id: "consignment",
    labelKey: "module_consignment",
    descKey: "module_consignmentDesc",
    permissions: [PP.Catalog.Read],
    comingSoon: true,
  },
];

export const MODULE_BY_ID: Record<PartnerBusinessModuleId, PartnerModuleDef> = Object.fromEntries(
  PARTNER_MODULES.map((m) => [m.id, m]),
) as Record<PartnerBusinessModuleId, PartnerModuleDef>;

/**
 * Resolve a partner's business module from existing participation mode + display-only mock labels.
 * No backend fields added — marketplaceCategory is mock-only for Commerce vs F&B split.
 */
export function resolvePartnerModule(partner: Partner): PartnerBusinessModuleId {
  if (partner.parkedModule === "consignment") {
    return "consignment";
  }
  switch (partner.participationMode) {
    case "SubscriptionFee":
      return "subscriptions";
    case "Principal":
      return partner.type === "Marketplace" ? "marketplace" : "delivery-service";
    case "ReflectionOnly":
      // Display-only mock split — maps to OfferingKind/field in production.
      return partner.marketplaceCategory === "fnb" ? "fnb" : "commerce";
    default:
      return "delivery-service";
  }
}

export function getModuleScreens(moduleId: PartnerBusinessModuleId): ModuleScreenDef[] {
  return PARTNER_MODULE_SCREENS[moduleId] ?? [];
}

export function getPartnerModuleScreens(partner: Partner): ModuleScreenDef[] {
  return getModuleScreens(resolvePartnerModule(partner));
}

export function getVisibleModuleScreens(
  moduleId: PartnerBusinessModuleId,
  granted: readonly string[],
): ModuleScreenDef[] {
  return getModuleScreens(moduleId).filter((screen) =>
    screen.permissions.some((p) => granted.includes(p) || granted.includes("Zahy.Admin")),
  );
}

export function partnersInModule(
  partners: Partner[],
  moduleId: PartnerBusinessModuleId,
): Partner[] {
  return partners.filter((p) => resolvePartnerModule(p) === moduleId);
}

export function moduleLabelKey(moduleId: PartnerBusinessModuleId): string {
  return MODULE_BY_ID[moduleId]?.labelKey ?? moduleId;
}

/** PSM and Platform Admin use partner-module navigation. */
export function canAccessPartnerModules(role: PortalRole): boolean {
  return role === "PlatformAdmin" || role === "PartnerSuccessManager";
}

export function canAccessMerchantPreview(role: PortalRole): boolean {
  return role === "PlatformAdmin" || role === "MerchantPreview";
}

/** Accountant Finance workspace — cross-module money view; not for PSM or Partner Finance. */
export function canAccessFinanceWorkspace(role: PortalRole): boolean {
  return role === "Accountant" || role === "PlatformAdmin";
}

export { defaultLandingPath } from "./roleNavConfig";

export type FinanceWorkspaceTabId =
  | "overview"
  | "reports"
  | "settlements"
  | "reversals"
  | "billing"
  | "balances"
  | "vat"
  | "pending-approvals"
  | "commission-approvals"
  | "reconcile";

export interface FinanceTabDef {
  id: FinanceWorkspaceTabId;
  path: string;
  labelKey: string;
}

export const FINANCE_WORKSPACE_TABS: readonly FinanceTabDef[] = [
  { id: "overview", path: "overview", labelKey: "financeTab_overview" },
  // Same i18n key as the sidebar Reports entry — one label source, one destination.
  { id: "reports", path: "reports", labelKey: "navReports" },
  { id: "settlements", path: "settlements", labelKey: "financeTab_settlements" },
  { id: "reversals", path: "reversals", labelKey: "financeTab_reversals" },
  { id: "billing", path: "billing", labelKey: "financeTab_billing" },
  { id: "balances", path: "balances", labelKey: "financeTab_balances" },
  { id: "vat", path: "vat", labelKey: "financeTab_vat" },
  { id: "pending-approvals", path: "pending-approvals", labelKey: "financeTab_pendingApprovals" },
  { id: "commission-approvals", path: "commission-approvals", labelKey: "financeTab_commissionApprovals" },
  { id: "reconcile", path: "reconcile", labelKey: "financeTab_aggregatorReconcile" },
];
