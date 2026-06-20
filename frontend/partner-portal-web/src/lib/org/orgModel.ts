/**
 * Multi-org permission model (MOCK, frontend only).
 *
 * Mirrors ABP Identity (users + permission groups + roles) and the existing tenant/partner
 * isolation so live wiring needs no rework:
 *   - Org.level Platform/Partner/Merchant ≈ host vs partner-scoped vs tenant-scoped.
 *   - Org.partnerId / Org.tenantId are the SAME ids used on Partner / MerchantActivation backend
 *     entities (partnerId, tenantId), so scope predicates map 1:1 to ABP data filters.
 *   - OrgUser.permissions is a flat permission set (role preset = seed, per-user overrides allowed),
 *     exactly like ABP role/permission grants.
 *
 * This file is PURE (no localStorage / no React) so the model can be unit-tested in isolation.
 */

export type OrgLevel = "Platform" | "Partner" | "Merchant";

export type OrgStatus = "Active" | "Suspended";

export interface Org {
  id: string;
  level: OrgLevel;
  name: string;
  /** Set for Partner orgs — equals the backend Partner.Id. */
  partnerId?: string;
  /** Set for Merchant orgs — equals the backend MerchantActivation.TenantId. */
  tenantId?: string;
  status: OrgStatus;
  createdAt: string;
}

/** The data-access identity derived from the signed-in user's org (drives every scope query). */
export interface OrgContext {
  orgId: string;
  level: OrgLevel;
  partnerId?: string;
  tenantId?: string;
}

export type OrgUserStatus = "Active" | "Invited" | "Suspended";

export interface OrgUser {
  id: string;
  orgId: string;
  name: string;
  email: string;
  /** Preset role inside the org — seeds permissions; per-user overrides allowed. */
  rolePreset: string;
  /** Effective, editable permission set (ABP-style flat grants). */
  permissions: string[];
  status: OrgUserStatus;
  invitedAt?: string;
  lastLoginAt?: string;
}

/* ------------------------------------------------------------------ */
/* Granular permission catalog (mirrors ABP permission groups)         */
/* ------------------------------------------------------------------ */

export const OrgPermissions = {
  PartnersView: "Partners.View",
  PartnersManage: "Partners.Manage",
  ActivationsView: "Activations.View",
  ActivationsRequest: "Activations.Request",
  ActivationsApprove: "Activations.Approve",
  SettlementView: "Settlement.View",
  ReversalsView: "Reversals.View",
  BillingView: "Billing.View",
  ReflectionView: "Reflection.View",
  CatalogView: "Catalog.View",
  CatalogManage: "Catalog.Manage",
  WebhooksManage: "Webhooks.Manage",
  CredentialsManage: "Credentials.Manage",
  UsersManage: "Users.Manage",
  OrgAccountsCreate: "OrgAccounts.Create",
} as const;

export type OrgPermission = (typeof OrgPermissions)[keyof typeof OrgPermissions];

export interface PermissionCatalogEntry {
  key: OrgPermission;
  group: string;
  /** Org levels for which this permission is meaningful (UI hides the rest). */
  levels: OrgLevel[];
}

const ALL: OrgLevel[] = ["Platform", "Partner", "Merchant"];

/** Authoritative catalog — what may be granted, grouped + restricted per org level. */
export const PERMISSION_CATALOG: readonly PermissionCatalogEntry[] = [
  { key: OrgPermissions.PartnersView, group: "Partners", levels: ["Platform"] },
  { key: OrgPermissions.PartnersManage, group: "Partners", levels: ["Platform"] },
  { key: OrgPermissions.OrgAccountsCreate, group: "Partners", levels: ["Platform"] },

  { key: OrgPermissions.ActivationsView, group: "Activations", levels: ALL },
  { key: OrgPermissions.ActivationsRequest, group: "Activations", levels: ["Partner", "Merchant"] },
  { key: OrgPermissions.ActivationsApprove, group: "Activations", levels: ["Platform"] },

  { key: OrgPermissions.SettlementView, group: "Finance", levels: ALL },
  { key: OrgPermissions.ReversalsView, group: "Finance", levels: ["Platform", "Partner"] },
  { key: OrgPermissions.BillingView, group: "Finance", levels: ALL },

  { key: OrgPermissions.ReflectionView, group: "Flows", levels: ALL },
  { key: OrgPermissions.CatalogView, group: "Catalog", levels: ["Platform", "Partner"] },
  { key: OrgPermissions.CatalogManage, group: "Catalog", levels: ["Platform", "Partner"] },

  { key: OrgPermissions.WebhooksManage, group: "Integration", levels: ["Platform", "Partner"] },
  { key: OrgPermissions.CredentialsManage, group: "Integration", levels: ["Platform", "Partner"] },

  { key: OrgPermissions.UsersManage, group: "Administration", levels: ALL },
];

export function permissionsForLevel(level: OrgLevel): OrgPermission[] {
  return PERMISSION_CATALOG.filter((p) => p.levels.includes(level)).map((p) => p.key);
}

/* ------------------------------------------------------------------ */
/* Role presets (seed a user's permission set; overrides allowed)      */
/* ------------------------------------------------------------------ */

export interface RolePresetDef {
  key: string;
  level: OrgLevel;
  permissions: OrgPermission[];
}

const P = OrgPermissions;

export const ROLE_PRESETS: readonly RolePresetDef[] = [
  // Platform org
  { key: "PlatformAdmin", level: "Platform", permissions: permissionsForLevel("Platform") },
  {
    key: "Accountant",
    level: "Platform",
    permissions: [
      P.PartnersView,
      P.ActivationsView,
      P.ActivationsApprove,
      P.SettlementView,
      P.ReversalsView,
      P.BillingView,
      P.ReflectionView,
    ],
  },
  // Partner org
  {
    key: "PartnerAdmin",
    level: "Partner",
    permissions: permissionsForLevel("Partner"),
  },
  {
    key: "PartnerFinance",
    level: "Partner",
    permissions: [
      P.ActivationsView,
      P.SettlementView,
      P.ReversalsView,
      P.BillingView,
      P.ReflectionView,
      P.CredentialsManage,
    ],
  },
  {
    key: "PartnerSuccess",
    level: "Partner",
    permissions: [
      P.ActivationsView,
      P.ActivationsRequest,
      P.ReflectionView,
      P.CatalogView,
      P.WebhooksManage,
    ],
  },
  // Merchant org
  {
    key: "MerchantAdmin",
    level: "Merchant",
    permissions: [
      P.ActivationsView,
      P.ActivationsRequest,
      P.SettlementView,
      P.BillingView,
      P.ReflectionView,
      P.UsersManage,
    ],
  },
  {
    key: "MerchantStaff",
    level: "Merchant",
    permissions: [P.ActivationsView, P.SettlementView, P.BillingView, P.ReflectionView],
  },
];

export function presetsForLevel(level: OrgLevel): RolePresetDef[] {
  return ROLE_PRESETS.filter((r) => r.level === level);
}

export function presetPermissions(level: OrgLevel, presetKey: string): OrgPermission[] {
  const preset = ROLE_PRESETS.find((r) => r.level === level && r.key === presetKey);
  return preset ? [...preset.permissions] : [];
}

/** A preset may only grant permissions valid for its org level (defence in depth). */
export function sanitizePermissionsForLevel(
  level: OrgLevel,
  permissions: readonly string[],
): OrgPermission[] {
  const allowed = new Set<string>(permissionsForLevel(level));
  return permissions.filter((p): p is OrgPermission => allowed.has(p));
}

/* ------------------------------------------------------------------ */
/* Layer 2 — per-user permission checks (within the org)               */
/* ------------------------------------------------------------------ */

export function userCan(user: Pick<OrgUser, "permissions" | "status">, permission: OrgPermission): boolean {
  if (user.status === "Suspended") return false;
  return user.permissions.includes(permission);
}

export function userCanAny(
  user: Pick<OrgUser, "permissions" | "status">,
  permissions: readonly OrgPermission[],
): boolean {
  if (permissions.length === 0) return true;
  return permissions.some((p) => userCan(user, p));
}

export function orgContextFromOrg(org: Org): OrgContext {
  return {
    orgId: org.id,
    level: org.level,
    partnerId: org.partnerId,
    tenantId: org.tenantId,
  };
}
