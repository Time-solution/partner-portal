import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  activeSelectionFor,
  endUsagePackageSelection,
  listUsagePackageSelections,
  resetUsagePackageSelections,
  selectUsagePackage,
} from "./usagePackageSelectionStore";
import { listUsagePackages, resetUsagePackages } from "./usagePackageStore";

// Seeded published packages (mirror the U2 store seeds).
const PARTNER_WHATSAPP = "22222222-2222-2222-2222-222222222004";
const PUBLISHED_RESALE = "pkg-wa-resale-10k";
const DRAFT_PAYG = "pkg-wa-payg";
const MERCHANT = "11111111-1111-1111-1111-111111111005";

describe("U4 usage package selection store", () => {
  beforeEach(() => {
    const mem: Record<string, string> = {};
    vi.stubGlobal("localStorage", {
      getItem: (k: string) => mem[k] ?? null,
      setItem: (k: string, v: string) => void (mem[k] = v),
      removeItem: (k: string) => void delete mem[k],
      clear: () => void Object.keys(mem).forEach((k) => delete mem[k]),
      key: () => null,
      length: 0,
    });
    resetUsagePackageSelections();
    resetUsagePackages();
  });

  it("selects a PUBLISHED package -> instant Active record", () => {
    const sel = selectUsagePackage({
      partnerId: PARTNER_WHATSAPP,
      usagePackageId: PUBLISHED_RESALE,
      tenantId: MERCHANT,
      merchantName: "Test Merchant",
      activatedAt: "2026-06-10",
    });

    expect(sel.status).toBe("Active"); // instant — no approval
    expect(sel.endedAt).toBeUndefined();
    expect(sel.activatedAt).toBe("2026-06-10");

    const active = activeSelectionFor(MERCHANT, PARTNER_WHATSAPP);
    expect(active?.usagePackageId).toBe(PUBLISHED_RESALE);
  });

  it("refuses to select a DRAFT (unpublished) package", () => {
    // The pay-as-you-go package is seeded as Draft — not browsable/selectable.
    expect(listUsagePackages().find((p) => p.id === DRAFT_PAYG)?.status).toBe("Draft");
    expect(() =>
      selectUsagePackage({
        partnerId: PARTNER_WHATSAPP,
        usagePackageId: DRAFT_PAYG,
        tenantId: MERCHANT,
        merchantName: "Test Merchant",
      }),
    ).toThrow(/published/i);
  });

  it("refuses an unknown package", () => {
    expect(() =>
      selectUsagePackage({
        partnerId: PARTNER_WHATSAPP,
        usagePackageId: "does-not-exist",
        tenantId: MERCHANT,
        merchantName: "Test Merchant",
      }),
    ).toThrow(/not found/i);
  });

  it("ends a selection mid-cycle (closes the active window)", () => {
    const sel = selectUsagePackage({
      partnerId: PARTNER_WHATSAPP,
      usagePackageId: PUBLISHED_RESALE,
      tenantId: MERCHANT,
      merchantName: "Test Merchant",
      activatedAt: "2026-06-01",
    });
    const ended = endUsagePackageSelection(sel.id, "2026-06-20");
    expect(ended.status).toBe("Ended");
    expect(ended.endedAt).toBe("2026-06-20");
    expect(activeSelectionFor(MERCHANT, PARTNER_WHATSAPP)).toBeUndefined();
  });

  it("scopes listings to the merchant tenant", () => {
    selectUsagePackage({
      partnerId: PARTNER_WHATSAPP,
      usagePackageId: PUBLISHED_RESALE,
      tenantId: MERCHANT,
      merchantName: "Test Merchant",
    });
    expect(listUsagePackageSelections(MERCHANT).length).toBe(1);
    expect(listUsagePackageSelections("99999999-9999-9999-9999-999999999999").length).toBe(0);
  });
});
