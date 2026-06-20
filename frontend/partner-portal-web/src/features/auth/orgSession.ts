/**
 * Bridge between the multi-org engine (Org + OrgUser + OrgContext) and the existing
 * PortalRole-based shell. PURE + testable.
 *
 * A signed-in org user resolves to BOTH:
 *   - a legacy PortalRole (drives the existing nav / route experience), and
 *   - an OrgContext (drives the proven data-scope engine + the new org screens).
 *
 * Keeping both in sync means the UI keeps working today while the org model is the
 * single source of truth for data access — ready for a clean live-swap to ABP Identity.
 */
import type { Org, OrgContext, OrgUser } from "@/lib/org/orgModel";
import { orgContextFromOrg } from "@/lib/org/orgModel";
import type { PortalRole } from "@/lib/rbac/portalRoles";
import { defaultLandingPath, partnerHomePath } from "@/lib/rbac/roleNavConfig";

export interface OrgSession {
  orgUser: OrgUser;
  org: Org;
  ctx: OrgContext;
  role: PortalRole;
  scopedPartnerId?: string;
  landing: string;
}

/** Map an org + its user onto the legacy PortalRole that drives the existing shell. */
export function legacyRoleForOrg(org: Org, user: OrgUser): PortalRole {
  if (org.level === "Platform") {
    return user.rolePreset === "Accountant" ? "Accountant" : "PlatformAdmin";
  }
  if (org.level === "Merchant") {
    return "MerchantPreview";
  }
  // Partner org
  return user.rolePreset === "PartnerFinance" ? "PartnerFinance" : "PartnerSuccessManager";
}

export function landingForOrg(org: Org, role: PortalRole): string {
  if (org.level === "Partner" && org.partnerId) {
    return partnerHomePath(org.partnerId);
  }
  return defaultLandingPath(role);
}

export function buildOrgSession(org: Org, orgUser: OrgUser): OrgSession {
  const role = legacyRoleForOrg(org, orgUser);
  return {
    orgUser,
    org,
    ctx: orgContextFromOrg(org),
    role,
    scopedPartnerId: org.level === "Partner" ? org.partnerId : undefined,
    landing: landingForOrg(org, role),
  };
}

/** Resolve a login by email against the org directory (case-insensitive). */
export function resolveLogin(
  orgs: readonly Org[],
  users: readonly OrgUser[],
  email: string,
): OrgSession | undefined {
  const normalized = email.trim().toLowerCase();
  const user = users.find((u) => u.email.trim().toLowerCase() === normalized);
  if (!user || user.status === "Suspended") return undefined;
  const org = orgs.find((o) => o.id === user.orgId);
  if (!org || org.status === "Suspended") return undefined;
  return buildOrgSession(org, user);
}

/**
 * Pick a representative org session for a legacy role — powers the in-shell role switcher
 * (demo convenience) so switching role also re-scopes the data layer correctly.
 */
export function sessionForRole(
  orgs: readonly Org[],
  users: readonly OrgUser[],
  role: PortalRole,
): OrgSession | undefined {
  for (const user of users) {
    if (user.status === "Suspended") continue;
    const org = orgs.find((o) => o.id === user.orgId);
    if (!org || org.status === "Suspended") continue;
    if (legacyRoleForOrg(org, user) === role) {
      return buildOrgSession(org, user);
    }
  }
  return undefined;
}
