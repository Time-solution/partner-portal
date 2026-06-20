import type { PortalRole } from "./portalRoles";

/** Mock partner scope — Salasa Delivery (PSM). */
export const MOCK_PARTNER_PSM_PARTNER_ID = "22222222-2222-2222-2222-222222222001";

/** Mock partner scope — Jahez Channel (Partner Finance). */
export const MOCK_PARTNER_FINANCE_PARTNER_ID = "22222222-2222-2222-2222-222222222004";

export type RoleExperience = "admin" | "partner" | "merchant" | "finance";

/** Nav item keys each role may see (single source of truth). */
export const ROLE_NAV_KEYS: Record<PortalRole, readonly string[]> = {
  PlatformAdmin: [
    "dashboard",
    "merchant-preview",
    "finance",
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
  Accountant: ["finance", "settings"],
  PartnerSuccessManager: ["partner-home", "webhooks", "credentials"],
  PartnerFinance: ["partner-home", "webhooks", "credentials"],
  MerchantPreview: ["merchant-preview"],
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
  return `/partners/${partnerId}`;
}

export function defaultLandingPath(role: PortalRole): string {
  switch (role) {
    case "MerchantPreview":
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

export function isPathAllowedForRole(pathname: string, role: PortalRole): boolean {
  const experience = roleExperience(role);

  // Self-service org users area is reachable by any signed-in org admin (org-permission gated downstream).
  const orgUsers = pathname === "/org-users" || pathname.startsWith("/org-users/");

  if (experience === "admin") return true;

  if (experience === "merchant") {
    return (
      pathname === "/merchant-preview" || pathname.startsWith("/merchant-preview/") || orgUsers
    );
  }

  if (experience === "finance") {
    return pathname.startsWith("/finance") || pathname.startsWith("/settings") || orgUsers;
  }

  if (experience === "partner") {
    const partnerId = mockScopedPartnerId(role)!;
    const ownPartner = pathname.startsWith(`/partners/${partnerId}`);
    const webhooks = pathname === "/webhooks" || pathname.startsWith("/webhooks/");
    const credentials = pathname === "/credentials" || pathname.startsWith("/credentials/");
    const partnerAlias = pathname === "/partner" || pathname.startsWith("/partner/");
    return ownPartner || webhooks || credentials || partnerAlias || orgUsers;
  }

  return false;
}
