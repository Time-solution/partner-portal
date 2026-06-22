import { describe, expect, it, beforeEach, vi } from "vitest";
import { createSeedData } from "@/lib/data/fixtures";
import { MockPortalDataSource } from "@/lib/data/mockDataSource";
import { savePersistedData } from "@/lib/data/mockStore";
import { PortalPermissions, rolePermissionMap } from "@/lib/rbac/portalRoles";
import { hasPermission } from "@/lib/permissions";

function stubLocalStorage() {
  const bag = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => bag.get(k) ?? null,
    setItem: (k: string, v: string) => bag.set(k, v),
    removeItem: (k: string) => bag.delete(k),
    clear: () => bag.clear(),
    key: () => null,
    length: 0,
  });
}

describe("CommissionApprovalsPage", () => {
  beforeEach(() => {
    stubLocalStorage();
    savePersistedData(createSeedData());
  });
  it("renders seeded entries in correct tabs", async () => {
    const ds = new MockPortalDataSource();
    const accrued = await ds.listCommissionLedger({ status: "Accrued" });
    expect(accrued.length).toBe(3);
    const approved = await ds.listCommissionLedger({ status: "Approved" });
    expect(approved.length).toBe(1);
    expect(approved[0].id).toBe("cl-app-chefz-burger-1");
    const reversed = await ds.listCommissionLedger({ status: "Reversed" });
    expect(reversed.length).toBe(1);
    expect(reversed[0].reversalReason).toContain("cancelled");
  });

  it("approve action moves entry to Approved tab", async () => {
    const ds = new MockPortalDataSource();
    await ds.approveCommissionEntry("cl-acc-salasa-burger-1", "ou-platform-accountant", "Omar Finance");
    const accrued = await ds.listCommissionLedger({ status: "Accrued" });
    expect(accrued.some((r) => r.id === "cl-acc-salasa-burger-1")).toBe(false);
    const approved = await ds.listCommissionLedger({ status: "Approved" });
    expect(approved.some((r) => r.id === "cl-acc-salasa-burger-1")).toBe(true);
  });

  it("mark paid button disabled when same actor as approver (mock rule)", async () => {
    const ds = new MockPortalDataSource();
    const accountantId = "ou-platform-accountant";
    await ds.approveCommissionEntry("cl-acc-whatsapp-burger-1", accountantId, "Omar Finance");
    await expect(ds.markCommissionPaid("cl-acc-whatsapp-burger-1", accountantId, "Omar Finance")).rejects.toThrow(
      /Two-person rule/,
    );
    await ds.markCommissionPaid("cl-acc-whatsapp-burger-1", "ou-platform-admin", "Admin User");
    const paid = await ds.listCommissionLedger({ status: "Paid" });
    expect(paid.some((r) => r.id === "cl-acc-whatsapp-burger-1")).toBe(true);
  });

  it("reverse modal requires min 10 char reason", async () => {
    const ds = new MockPortalDataSource();
    await expect(ds.reverseCommissionEntry("cl-acc-oto-pizza-1", "short", "Omar")).rejects.toThrow(/10 characters/);
    const rev = await ds.reverseCommissionEntry(
      "cl-acc-oto-pizza-1",
      "Order cancelled by customer post-fulfillment",
      "Omar Finance",
    );
    expect(rev.entryKind).toBe("Reversal");
    expect(rev.status).toBe("Reversed");
  });

  it("page hidden from partner and merchant roles (permission)", () => {
    expect(hasPermission(rolePermissionMap.PartnerFinance, PortalPermissions.Commission.Approve)).toBe(false);
    expect(hasPermission(rolePermissionMap.MerchantPreview, PortalPermissions.Commission.Approve)).toBe(false);
    expect(hasPermission(rolePermissionMap.Accountant, PortalPermissions.Commission.Approve)).toBe(true);
  });
});

describe("commission ledger seed", () => {
  it("includes five workflow states on first load", () => {
    const data = createSeedData();
    expect(data.commissionLedger.length).toBeGreaterThanOrEqual(5);
  });
});
