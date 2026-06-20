export type PortalRole =
  | "PlatformAdmin"
  | "Accountant"
  | "PartnerSuccessManager"
  | "PartnerFinance";

export const PORTAL_ROLES: PortalRole[] = [
  "PlatformAdmin",
  "Accountant",
  "PartnerSuccessManager",
  "PartnerFinance",
];

export const PortalPermissions = {
  Dashboard: { Read: "Zahy.Portal.Dashboard.Read" },
  Partners: { Read: "Zahy.Portal.Partners.Read", Manage: "Zahy.Portal.Partners.Manage" },
  Catalog: { Read: "Zahy.Portal.Catalog.Read", Write: "Zahy.Portal.Catalog.Write" },
  Activations: {
    Read: "Zahy.Portal.Activations.Read",
    Request: "Zahy.Portal.Activations.Request",
    Approve: "Zahy.Portal.Activations.Approve",
  },
  Settlement: { Read: "Zahy.Portal.Settlement.Read", Reconcile: "Zahy.Portal.Settlement.Reconcile" },
  Disburse: { Execute: "Zahy.Portal.Disburse.Execute" },
  Reflection: { Read: "Zahy.Portal.Reflection.Read" },
  Billing: {
    Read: "Zahy.Portal.Billing.Read",
    SetTerms: "Zahy.Portal.Billing.SetTerms",
  },
  Reversals: { Read: "Zahy.Portal.Reversals.Read" },
  Finance: { Read: "Zahy.Portal.Finance.Read" },
  Webhooks: { Read: "Zahy.Portal.Webhooks.Read", Manage: "Zahy.Portal.Webhooks.Manage" },
  Credentials: { Read: "Zahy.Portal.Credentials.Read", Rotate: "Zahy.Portal.Credentials.Rotate" },
  Settings: { Read: "Zahy.Portal.Settings.Read", Manage: "Zahy.Portal.Settings.Manage" },
  Users: { Read: "Zahy.Portal.Users.Read", Manage: "Zahy.Portal.Users.Manage" },
  PartnerFinance: { ReadOwn: "Zahy.Portal.PartnerFinance.ReadOwn" },
} as const;

const ALL = Object.values(PortalPermissions).flatMap((g) => Object.values(g));

export const rolePermissionMap: Record<PortalRole, readonly string[]> = {
  PlatformAdmin: ALL,
  Accountant: [
    PortalPermissions.Dashboard.Read,
    PortalPermissions.Finance.Read,
    PortalPermissions.Partners.Read,
    PortalPermissions.Catalog.Read,
    PortalPermissions.Activations.Read,
    PortalPermissions.Settlement.Read,
    PortalPermissions.Settlement.Reconcile,
    PortalPermissions.Billing.Read,
    PortalPermissions.Billing.SetTerms,
    PortalPermissions.Reversals.Read,
    PortalPermissions.Webhooks.Read,
    PortalPermissions.Credentials.Read,
    PortalPermissions.Settings.Read,
  ],
  PartnerSuccessManager: [
    PortalPermissions.Dashboard.Read,
    PortalPermissions.Partners.Read,
    PortalPermissions.Partners.Manage,
    PortalPermissions.Catalog.Read,
    PortalPermissions.Activations.Read,
    PortalPermissions.Activations.Request,
    PortalPermissions.Reflection.Read,
    PortalPermissions.Webhooks.Read,
    PortalPermissions.Webhooks.Manage,
    PortalPermissions.Credentials.Read,
  ],
  PartnerFinance: [
    PortalPermissions.Dashboard.Read,
    PortalPermissions.PartnerFinance.ReadOwn,
    PortalPermissions.Billing.Read,
    PortalPermissions.Settlement.Read,
    PortalPermissions.Webhooks.Read,
    PortalPermissions.Credentials.Read,
    PortalPermissions.Credentials.Rotate,
  ],
};

export const roleLabels: Record<PortalRole, { en: string; ar: string }> = {
  PlatformAdmin: { en: "Platform Admin", ar: "مسؤول المنصة" },
  Accountant: { en: "Accountant", ar: "محاسب" },
  PartnerSuccessManager: { en: "Partner Success", ar: "نجاح الشركاء" },
  PartnerFinance: { en: "Partner Finance", ar: "مالية الشريك" },
};

export const roleBadgeClass: Record<PortalRole, string> = {
  PlatformAdmin: "bg-primary/15 text-primary border-primary/30",
  Accountant: "bg-sky-500/15 text-sky-700 dark:text-sky-300 border-sky-500/30",
  PartnerSuccessManager: "bg-orange-500/15 text-orange-700 dark:text-orange-300 border-orange-500/30",
  PartnerFinance: "bg-violet-500/15 text-violet-700 dark:text-violet-300 border-violet-500/30",
};

export function permissionsForRole(role: PortalRole): readonly string[] {
  return rolePermissionMap[role];
}

export function canDisburse(role: PortalRole): boolean {
  return role === "PlatformAdmin";
}

const ROLE_RANK: Record<PortalRole, number> = {
  PartnerFinance: 1,
  PartnerSuccessManager: 2,
  Accountant: 3,
  PlatformAdmin: 4,
};

/** PartnerFinance is the only role scoped to a single partner in the portal. */
export function roleRequiresPartner(role: PortalRole): boolean {
  return role === "PartnerFinance";
}

/** Lower roles cannot grant higher roles (PlatformAdmin may grant any). */
export function canGrantRole(grantorRole: PortalRole, targetRole: PortalRole): boolean {
  return ROLE_RANK[grantorRole] >= ROLE_RANK[targetRole];
}
