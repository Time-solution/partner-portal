export type PortalRole =
  | "PlatformAdmin"
  | "Accountant"
  | "PartnerSuccessManager"
  | "PartnerFinance"
  | "MerchantPreview";

export const PORTAL_ROLES: PortalRole[] = [
  "PlatformAdmin",
  "Accountant",
  "PartnerSuccessManager",
  "PartnerFinance",
];

/** Mock demo roles — includes preview-only roles not used in production portal login. */
export const MOCK_DEMO_ROLES: PortalRole[] = [...PORTAL_ROLES, "MerchantPreview"];

export const PortalPermissions = {
  Dashboard: { Read: "Zahy.Portal.Dashboard.Read" },
  Partners: { Read: "Zahy.Portal.Partners.Read", Manage: "Zahy.Portal.Partners.Manage" },
  Catalog: {
    Read: "Zahy.Portal.Catalog.Read",
    Write: "Zahy.Portal.Catalog.Write",
    AuthorSelf: "Zahy.Portal.Catalog.Author.Self",
    AuthorManaged: "Zahy.Portal.Catalog.Author.Managed",
  },
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
  Finance: {
    Read: "Zahy.Portal.Finance.Read",
    /** Platform-only: issue an ad-hoc (manual) invoice with keyed line items. */
    WriteManualInvoice: "Zahy.Portal.Finance.WriteManualInvoice",
  },
  Commission: { Approve: "Zahy.Commission.Approve" },
  Webhooks: { Read: "Zahy.Portal.Webhooks.Read", Manage: "Zahy.Portal.Webhooks.Manage" },
  Credentials: { Read: "Zahy.Portal.Credentials.Read", Rotate: "Zahy.Portal.Credentials.Rotate" },
  Settings: { Read: "Zahy.Portal.Settings.Read", Manage: "Zahy.Portal.Settings.Manage" },
  /** Track B — accountant-managed bank registry. Read lists banks; Manage add/edit/deactivate. */
  Banks: { Read: "Zahy.Portal.Banks.Read", Manage: "Zahy.Portal.Banks.Manage" },
  Users: { Read: "Zahy.Portal.Users.Read", Manage: "Zahy.Portal.Users.Manage" },
  PartnerFinance: { ReadOwn: "Zahy.Portal.PartnerFinance.ReadOwn" },
  MerchantPreview: { Read: "Zahy.Portal.MerchantPreview.Read" },
} as const;

const ALL = Object.values(PortalPermissions).flatMap((g) => Object.values(g));

export const rolePermissionMap: Record<PortalRole, readonly string[]> = {
  PlatformAdmin: ALL,
  Accountant: [
    PortalPermissions.Dashboard.Read,
    PortalPermissions.Finance.Read,
    PortalPermissions.Finance.WriteManualInvoice,
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
    PortalPermissions.Banks.Read,
    PortalPermissions.Banks.Manage,
    PortalPermissions.Commission.Approve,
  ],
  PartnerSuccessManager: [
    PortalPermissions.Partners.Read,
    PortalPermissions.Catalog.Read,
    PortalPermissions.Catalog.AuthorManaged,
    PortalPermissions.Activations.Read,
    PortalPermissions.Activations.Request,
    PortalPermissions.Reflection.Read,
    PortalPermissions.Settlement.Read,
    PortalPermissions.Reversals.Read,
    PortalPermissions.Webhooks.Read,
    PortalPermissions.Webhooks.Manage,
    PortalPermissions.Credentials.Read,
  ],
  PartnerFinance: [
    PortalPermissions.PartnerFinance.ReadOwn,
    PortalPermissions.Billing.Read,
    PortalPermissions.Settlement.Read,
    PortalPermissions.Catalog.Read,
    PortalPermissions.Catalog.AuthorSelf,
    PortalPermissions.Activations.Read,
    PortalPermissions.Reflection.Read,
    PortalPermissions.Reversals.Read,
    PortalPermissions.Webhooks.Read,
    PortalPermissions.Credentials.Read,
    PortalPermissions.Credentials.Rotate,
  ],
  MerchantPreview: [PortalPermissions.MerchantPreview.Read],
};

export const roleLabels: Record<PortalRole, { en: string; ar: string }> = {
  PlatformAdmin: { en: "Platform Admin", ar: "مسؤول المنصة" },
  Accountant: { en: "Accountant", ar: "محاسب" },
  PartnerSuccessManager: { en: "Partner Success", ar: "نجاح الشركاء" },
  PartnerFinance: { en: "Partner Finance", ar: "مالية الشريك" },
  MerchantPreview: { en: "Merchant (preview)", ar: "تاجر (معاينة)" },
};

export const roleBadgeClass: Record<PortalRole, string> = {
  PlatformAdmin: "bg-primary/15 text-primary border-primary/30",
  Accountant: "bg-sky-500/15 text-sky-700 dark:text-sky-300 border-sky-500/30",
  PartnerSuccessManager: "bg-orange-500/15 text-orange-700 dark:text-orange-300 border-orange-500/30",
  PartnerFinance: "bg-violet-500/15 text-violet-700 dark:text-violet-300 border-violet-500/30",
  MerchantPreview: "bg-teal-500/15 text-teal-700 dark:text-teal-300 border-teal-500/30",
};

export function permissionsForRole(role: PortalRole): readonly string[] {
  return rolePermissionMap[role];
}

export function canDisburse(role: PortalRole): boolean {
  return role === "PlatformAdmin";
}

/**
 * Two-person rule (separation of duties) for releasing a disbursement — mirrors the backend
 * `Disbursement.Release` enforcement. The releaser must (1) hold the Disburse privilege AND (2) NOT be the
 * same human who reconciled the batch. Even a PlatformAdmin holding both permissions is blocked from doing
 * BOTH gates on the SAME item: the rule compares actor ids (case-insensitive / trimmed), not just roles.
 */
export function canReleaseDisbursement(opts: {
  releaserRole: PortalRole;
  releaserActorId: string;
  reconciledByActorId: string | null | undefined;
}): boolean {
  if (!canDisburse(opts.releaserRole)) return false;
  const releaser = (opts.releaserActorId ?? "").trim().toLowerCase();
  if (!releaser) return false;
  const reconciler = (opts.reconciledByActorId ?? "").trim().toLowerCase();
  return releaser !== reconciler;
}

const ROLE_RANK: Record<PortalRole, number> = {
  MerchantPreview: 0,
  PartnerFinance: 1,
  PartnerSuccessManager: 2,
  Accountant: 3,
  PlatformAdmin: 4,
};

/** Partner portal roles scoped to a single partner (mock + live-swap ready). */
export function roleRequiresPartner(role: PortalRole): boolean {
  return role === "PartnerFinance" || role === "PartnerSuccessManager";
}

/** Lower roles cannot grant higher roles (PlatformAdmin may grant any). */
export function canGrantRole(grantorRole: PortalRole, targetRole: PortalRole): boolean {
  return ROLE_RANK[grantorRole] >= ROLE_RANK[targetRole];
}
