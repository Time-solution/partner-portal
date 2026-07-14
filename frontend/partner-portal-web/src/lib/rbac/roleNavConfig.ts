import type { PortalRole } from "./portalRoles";

/** Mock partner scope — Salasa Delivery (PSM). */
export const MOCK_PARTNER_PSM_PARTNER_ID = "22222222-2222-2222-2222-222222222001";

/** Mock partner scope — WhatsApp Co (Partner Finance). */
export const MOCK_PARTNER_FINANCE_PARTNER_ID = "22222222-2222-2222-2222-222222222004";

export type RoleExperience = "admin" | "partner" | "merchant" | "finance";

/** Nav item keys each role may see (single source of truth). */
export const ROLE_NAV_KEYS: Record<PortalRole, readonly string[]> = {
  PlatformAdmin: [
    "dashboard",
    "merchant-preview",
    "finance",
    "reports",
    "commission-approvals",
    "manual-invoice-new",
    "delivery-service",
    "commerce",
    "fnb",
    "subscriptions",
    "marketplace",
    "consignment",
    "partners",
    "webhooks",
    "credentials",
    "settings",
  ],
  Accountant: ["finance", "reports", "commission-approvals", "manual-invoice-new", "settings"],
  // Partner sidebar — direct entries into the partner-scoped screens (no cross-role leakage:
  // these keys exist only here, and every path resolves under /partners/{ownId}/…).
  PartnerSuccessManager: [
    "partner-home",
    "partner-catalog",
    "partner-packages",
    "partner-activations",
    "partner-orders",
    "partner-statements",
    "webhooks",
    "credentials",
    "partner-settings",
  ],
  PartnerFinance: [
    "partner-home",
    "partner-catalog",
    "partner-packages",
    "partner-activations",
    "partner-orders",
    "partner-statements",
    "webhooks",
    "credentials",
    "partner-settings",
  ],
  // Merchant sidebar — the query-param tabs promoted to real nav entries (params keep working).
  MerchantPreview: [
    "merchant-dashboard",
    "merchant-browse",
    "merchant-services",
    "merchant-orders",
    "merchant-invoices",
    "merchant-statement",
    "merchant-profile",
  ],
};

export function roleExperience(role: PortalRole): RoleExperience {
  if (role === "MerchantPreview") return "merchant";
  if (role === "Accountant") return "finance";
  if (role === "PartnerSuccessManager" || role === "PartnerFinance") return "partner";
  return "admin";
}

export function isPartnerScopedRole(role: PortalRole | string): boolean {
  return role === "PartnerSuccessManager" || role === "PartnerFinance";
}

export function mockScopedPartnerId(role: PortalRole | string): string | undefined {
  if (role === "PartnerFinance") return MOCK_PARTNER_FINANCE_PARTNER_ID;
  if (role === "PartnerSuccessManager") return MOCK_PARTNER_PSM_PARTNER_ID;
  return undefined;
}

export function partnerHomePath(partnerId: string): string {
  return `/partners/${partnerId}/active-merchants`;
}

export function defaultLandingPath(role: PortalRole): string {
  switch (role) {
    case "MerchantPreview":
      // Lands on the merchant dashboard; legacy ?tab=partners still resolves (services alias).
      return "/merchant-preview";
    case "Accountant":
      return "/finance/overview";
    case "PartnerSuccessManager":
      return partnerHomePath(MOCK_PARTNER_PSM_PARTNER_ID);
    case "PartnerFinance":
      return partnerHomePath(MOCK_PARTNER_FINANCE_PARTNER_ID);
    default:
      return "/dashboard";
  }
}

/** Platform-wide admin routes partner / merchant roles must not access. */
export const ADMIN_ONLY_PATH_PREFIXES = [
  "/dashboard",
  "/modules",
  "/partners",
  "/settings",
  "/finance",
  "/merchant-preview",
] as const;

export function isPathAllowedForRole(
  pathname: string,
  role: PortalRole,
  /**
   * The ACTUAL signed-in partner scope (from the org session). Falls back to the
   * legacy demo mapping only when not supplied. Passing it hardens the guard so a
   * partner login (e.g. Chefz) can never reach another partner's URL by direct entry.
   */
  scopedPartnerId?: string,
): boolean {
  const experience = roleExperience(role);

  // Self-service org users area is reachable by any signed-in org admin (org-permission gated downstream).
  const orgUsers = pathname === "/org-users" || pathname.startsWith("/org-users/");

  if (experience === "admin") return true;

  if (experience === "merchant") {
    return (
      pathname === "/merchant-preview" ||
      pathname.startsWith("/merchant-preview/") ||
      pathname.startsWith("/merchant-preview?") ||
      orgUsers
    );
  }

  if (experience === "finance") {
    return pathname.startsWith("/finance") || pathname.startsWith("/settings") || orgUsers;
  }

  if (experience === "partner") {
    const partnerId = scopedPartnerId ?? mockScopedPartnerId(role)!;
    const ownPartner = pathname.startsWith(`/partners/${partnerId}`);
    const webhooks = pathname === "/webhooks" || pathname.startsWith("/webhooks/");
    const credentials = pathname === "/credentials" || pathname.startsWith("/credentials/");
    const partnerAlias = pathname === "/partner" || pathname.startsWith("/partner/");
    return ownPartner || webhooks || credentials || partnerAlias || orgUsers;
  }

  return false;
}
