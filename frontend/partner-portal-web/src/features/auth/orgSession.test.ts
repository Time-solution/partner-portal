import { describe, expect, it } from "vitest";
import type { Org, OrgUser } from "@/lib/org/orgModel";
import {
  MOCK_PARTNER_FINANCE_PARTNER_ID,
  MOCK_PARTNER_PSM_PARTNER_ID,
  partnerHomePath,
} from "@/lib/rbac/roleNavConfig";
import {
  buildOrgSession,
  legacyRoleForOrg,
  resolveLogin,
  sessionForRole,
} from "./orgSession";

const orgs: Org[] = [
  { id: "org-platform", level: "Platform", name: "Zahy Platform", status: "Active", createdAt: "" },
  {
    id: "org-salasa",
    level: "Partner",
    name: "Salasa",
    partnerId: MOCK_PARTNER_PSM_PARTNER_ID,
    status: "Active",
    createdAt: "",
  },
  {
    id: "org-jahez",
    level: "Partner",
    name: "Jahez",
    partnerId: MOCK_PARTNER_FINANCE_PARTNER_ID,
    status: "Active",
    createdAt: "",
  },
  {
    id: "org-merchant",
    level: "Merchant",
    name: "Quick Bites",
    tenantId: "tenant-qb",
    status: "Active",
    createdAt: "",
  },
  { id: "org-suspended", level: "Partner", name: "Closed Co", partnerId: "p-x", status: "Suspended", createdAt: "" },
];

const u = (over: Partial<OrgUser> & Pick<OrgUser, "id" | "orgId" | "email" | "rolePreset">): OrgUser => ({
  name: over.email,
  permissions: [],
  status: "Active",
  ...over,
});

const users: OrgUser[] = [
  u({ id: "u-admin", orgId: "org-platform", email: "admin@zahy.sa", rolePreset: "PlatformAdmin" }),
  u({ id: "u-acc", orgId: "org-platform", email: "accountant@zahy.sa", rolePreset: "Accountant" }),
  u({ id: "u-salasa", orgId: "org-salasa", email: "owner@salasa.sa", rolePreset: "PartnerAdmin" }),
  u({ id: "u-jahez", orgId: "org-jahez", email: "finance@jahez.sa", rolePreset: "PartnerFinance" }),
  u({ id: "u-merch", orgId: "org-merchant", email: "owner@qb.sa", rolePreset: "MerchantAdmin" }),
  u({ id: "u-susp", orgId: "org-platform", email: "gone@zahy.sa", rolePreset: "Accountant", status: "Suspended" }),
  u({ id: "u-orphan", orgId: "org-suspended", email: "x@x.sa", rolePreset: "PartnerAdmin" }),
];

describe("legacyRoleForOrg", () => {
  it("maps platform presets", () => {
    expect(legacyRoleForOrg(orgs[0], users[0])).toBe("PlatformAdmin");
    expect(legacyRoleForOrg(orgs[0], users[1])).toBe("Accountant");
  });
  it("maps partner presets (admin/success → PSM, finance → PartnerFinance)", () => {
    expect(legacyRoleForOrg(orgs[1], users[2])).toBe("PartnerSuccessManager");
    expect(legacyRoleForOrg(orgs[2], users[3])).toBe("PartnerFinance");
  });
  it("maps merchant to MerchantPreview", () => {
    expect(legacyRoleForOrg(orgs[3], users[4])).toBe("MerchantPreview");
  });
});

describe("buildOrgSession", () => {
  it("partner session carries scopedPartnerId + partner landing", () => {
    const s = buildOrgSession(orgs[1], users[2]);
    expect(s.role).toBe("PartnerSuccessManager");
    expect(s.scopedPartnerId).toBe(MOCK_PARTNER_PSM_PARTNER_ID);
    expect(s.ctx).toEqual({
      orgId: "org-salasa",
      level: "Partner",
      partnerId: MOCK_PARTNER_PSM_PARTNER_ID,
      tenantId: undefined,
    });
    expect(s.landing).toBe(partnerHomePath(MOCK_PARTNER_PSM_PARTNER_ID));
  });
  it("platform session has no scopedPartnerId and dashboard landing", () => {
    const s = buildOrgSession(orgs[0], users[0]);
    expect(s.scopedPartnerId).toBeUndefined();
    expect(s.landing).toBe("/dashboard");
  });
  it("merchant session carries tenant ctx", () => {
    const s = buildOrgSession(orgs[3], users[4]);
    expect(s.ctx.tenantId).toBe("tenant-qb");
    expect(s.landing).toBe("/merchant-preview?tab=partners");
  });
});

describe("resolveLogin", () => {
  it("authenticates a known active user (case-insensitive)", () => {
    const s = resolveLogin(orgs, users, "OWNER@salasa.SA");
    expect(s?.org.id).toBe("org-salasa");
    expect(s?.role).toBe("PartnerSuccessManager");
  });
  it("rejects unknown email", () => {
    expect(resolveLogin(orgs, users, "nobody@zahy.sa")).toBeUndefined();
  });
  it("rejects suspended user", () => {
    expect(resolveLogin(orgs, users, "gone@zahy.sa")).toBeUndefined();
  });
  it("rejects user whose org is suspended", () => {
    expect(resolveLogin(orgs, users, "x@x.sa")).toBeUndefined();
  });
});

describe("sessionForRole (role switcher)", () => {
  it("finds a representative session per legacy role", () => {
    expect(sessionForRole(orgs, users, "PlatformAdmin")?.org.id).toBe("org-platform");
    expect(sessionForRole(orgs, users, "Accountant")?.orgUser.email).toBe("accountant@zahy.sa");
    expect(sessionForRole(orgs, users, "PartnerSuccessManager")?.org.id).toBe("org-salasa");
    expect(sessionForRole(orgs, users, "PartnerFinance")?.org.id).toBe("org-jahez");
    expect(sessionForRole(orgs, users, "MerchantPreview")?.org.id).toBe("org-merchant");
  });
});
