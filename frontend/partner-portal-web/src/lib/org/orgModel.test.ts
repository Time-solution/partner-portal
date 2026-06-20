import { describe, expect, it } from "vitest";
import type { SettlementCase, SettlementJournal } from "@/lib/data/types";
import {
  OrgPermissions,
  PERMISSION_CATALOG,
  permissionsForLevel,
  presetPermissions,
  presetsForLevel,
  sanitizePermissionsForLevel,
  userCan,
  userCanAny,
  type OrgContext,
  type OrgUser,
} from "./orgModel";
import { canSeeSharedFlow } from "./orgScope";

const journal: SettlementJournal = {
  currency: "SAR",
  totalDebits: { amount: 0, currency: "SAR", vatInclusive: false },
  totalCredits: { amount: 0, currency: "SAR", vatInclusive: false },
  lines: [],
};

describe("permission catalog mirrors ABP groups, restricted per level", () => {
  it("Activations.Approve and Partners.Manage are platform-only", () => {
    expect(permissionsForLevel("Platform")).toContain(OrgPermissions.ActivationsApprove);
    expect(permissionsForLevel("Partner")).not.toContain(OrgPermissions.ActivationsApprove);
    expect(permissionsForLevel("Merchant")).not.toContain(OrgPermissions.PartnersManage);
  });

  it("merchants cannot be granted partner credential/catalog management", () => {
    const merchantPerms = permissionsForLevel("Merchant");
    expect(merchantPerms).not.toContain(OrgPermissions.CredentialsManage);
    expect(merchantPerms).not.toContain(OrgPermissions.CatalogManage);
  });

  it("every catalog entry has a known group + at least one level", () => {
    for (const entry of PERMISSION_CATALOG) {
      expect(entry.levels.length).toBeGreaterThan(0);
      expect(entry.group).toBeTruthy();
    }
  });
});

describe("role presets seed sane permission sets", () => {
  it("PlatformAdmin gets every platform permission", () => {
    expect(presetPermissions("Platform", "PlatformAdmin").sort()).toEqual(
      permissionsForLevel("Platform").sort(),
    );
  });

  it("each org level exposes its own presets only", () => {
    expect(presetsForLevel("Partner").map((p) => p.key)).toContain("PartnerFinance");
    expect(presetsForLevel("Merchant").map((p) => p.key)).not.toContain("PartnerFinance");
  });

  it("sanitize strips permissions not valid for the level", () => {
    const sanitized = sanitizePermissionsForLevel("Merchant", [
      OrgPermissions.SettlementView,
      OrgPermissions.CredentialsManage, // invalid for merchant
    ]);
    expect(sanitized).toContain(OrgPermissions.SettlementView);
    expect(sanitized).not.toContain(OrgPermissions.CredentialsManage);
  });
});

describe("layer 2 — per-user permission checks", () => {
  const financeUser: OrgUser = {
    id: "u1",
    orgId: "org-partner",
    name: "Finance",
    email: "f@p.sa",
    rolePreset: "PartnerFinance",
    permissions: presetPermissions("Partner", "PartnerFinance"),
    status: "Active",
  };

  it("grants permissions in the set", () => {
    expect(userCan(financeUser, OrgPermissions.SettlementView)).toBe(true);
  });

  it("denies permissions not in the set", () => {
    expect(userCan(financeUser, OrgPermissions.WebhooksManage)).toBe(false);
  });

  it("suspended users are denied everything", () => {
    expect(userCan({ ...financeUser, status: "Suspended" }, OrgPermissions.SettlementView)).toBe(false);
  });

  it("userCanAny works across a set", () => {
    expect(userCanAny(financeUser, [OrgPermissions.WebhooksManage, OrgPermissions.BillingView])).toBe(true);
    expect(userCanAny(financeUser, [OrgPermissions.WebhooksManage, OrgPermissions.PartnersManage])).toBe(false);
  });
});

describe("two-layer enforcement (org scope AND user permission)", () => {
  const PA = "partner-A";
  const TM = "tenant-M";
  const ctxPartnerA: OrgContext = { orgId: "org-A", level: "Partner", partnerId: PA };

  const sharedCase: SettlementCase = {
    id: "cs-AM",
    partnerId: PA,
    partnerName: PA,
    tenantId: TM,
    externalTransactionId: "order:cs-AM:v1",
    book: "Marketplace",
    state: "Allocated",
    journal,
    createdAt: "2026-06-01T00:00:00Z",
  };

  const withSettlement: OrgUser = {
    id: "u2",
    orgId: "org-A",
    name: "Viewer",
    email: "v@a.sa",
    rolePreset: "PartnerFinance",
    permissions: [OrgPermissions.SettlementView],
    status: "Active",
  };
  const withoutSettlement: OrgUser = { ...withSettlement, id: "u3", permissions: [] };

  const canView = (user: OrgUser) =>
    canSeeSharedFlow(sharedCase, ctxPartnerA) && userCan(user, OrgPermissions.SettlementView);

  it("visible record + permission => allowed", () => {
    expect(canView(withSettlement)).toBe(true);
  });

  it("visible record but NO permission => blocked (layer 2)", () => {
    expect(canView(withoutSettlement)).toBe(false);
  });

  it("permission but record NOT in scope => blocked (layer 1)", () => {
    const ctxOtherPartner: OrgContext = { orgId: "org-B", level: "Partner", partnerId: "partner-B" };
    const blocked = canSeeSharedFlow(sharedCase, ctxOtherPartner) && userCan(withSettlement, OrgPermissions.SettlementView);
    expect(blocked).toBe(false);
  });
});
