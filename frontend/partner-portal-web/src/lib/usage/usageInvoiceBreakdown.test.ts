import { describe, expect, it } from "vitest";
import { VAT_RATE, splitInclusiveVat } from "@/lib/reports/settlementReports";
import { computeUsageBilling } from "./usageBilling";
import {
  activeDaysInPeriod,
  buildUsageInvoiceBreakdown,
} from "./usageInvoiceBreakdown";
import type { UsagePackage } from "./usagePackage";

// Resale package matching the prompt's worked example: base 100, included 5,000, overage 0.05 (sell side).
const RESALE: UsagePackage = {
  id: "pkg-resale",
  partnerId: "22222222-2222-2222-2222-222222222004",
  name: "Resale 5k",
  unitLabel: "messages",
  mode: "Resale",
  currency: "SAR",
  includedQuantity: 5000,
  baseBuyAmount: 60,
  baseSellAmount: 100,
  overageBuyAmount: 0.03,
  overageSellAmount: 0.05,
  payer: "Merchant",
  status: "Published",
};

const SUBSCRIPTION: UsagePackage = {
  id: "pkg-sub",
  partnerId: "22222222-2222-2222-2222-222222222008",
  name: "Subscription 5k",
  unitLabel: "messages",
  mode: "Subscription",
  currency: "SAR",
  includedQuantity: 5000,
  baseBuyAmount: 0,
  baseSellAmount: 149,
  overageBuyAmount: 0,
  overageSellAmount: 0.03,
  payer: "Partner",
  status: "Published",
};

describe("U4 invoice breakdown — active window days", () => {
  it("full month when active before the month with no end", () => {
    expect(activeDaysInPeriod("2026-06", "2026-06-01")).toBe(30);
    expect(activeDaysInPeriod("2026-06", "2026-05-20")).toBe(30);
  });

  it("counts inclusive days from a mid-month activation", () => {
    // 16 Jun .. 30 Jun inclusive = 15 days.
    expect(activeDaysInPeriod("2026-06", "2026-06-16")).toBe(15);
  });

  it("counts to the deactivation day", () => {
    // 1 Jun .. 20 Jun inclusive = 20 days.
    expect(activeDaysInPeriod("2026-06", "2026-06-01", "2026-06-20")).toBe(20);
  });
});

describe("U4 invoice breakdown — base + overage, ties to U3", () => {
  it("merchant sees the SELL breakdown (base + excess × rate = total); buy/margin absent", () => {
    const b = buildUsageInvoiceBreakdown(RESALE, 6200, "2026-06", { viewer: "merchant", activatedAt: "2026-06-01" });
    const u3 = computeUsageBilling(RESALE, 6200, "2026-06");

    expect(b.legKind).toBe("sell");
    expect(b.baseInclusive).toBe(100);
    expect(b.overageExcess).toBe(1200); // 6200 − 5000
    expect(b.overageRate).toBe(0.05);
    expect(b.overageInclusive).toBe(60); // 1200 × 0.05
    expect(b.totalInclusive).toBe(160); // 100 + 60

    // No recompute — numbers come straight from U3's sell leg.
    expect(b.totalInclusive).toBe(u3.sell!.totalInclusive);
    expect(b.overageInclusive).toBe(u3.sell!.overageInclusive);

    // VAT split via the single helper.
    const { exVat, vat } = splitInclusiveVat(160, VAT_RATE);
    expect(b.exVat).toBe(exVat);
    expect(b.vat).toBe(vat);

    // Buy/margin are structurally absent for the merchant.
    expect(b.marginInclusive).toBeUndefined();
  });

  it("partner sees the BUY breakdown; sell/margin absent", () => {
    const b = buildUsageInvoiceBreakdown(RESALE, 6200, "2026-06", { viewer: "partner", activatedAt: "2026-06-01" });
    const u3 = computeUsageBilling(RESALE, 6200, "2026-06");

    expect(b.legKind).toBe("buy");
    expect(b.baseInclusive).toBe(60);
    expect(b.overageRate).toBe(0.03);
    expect(b.overageInclusive).toBe(36); // 1200 × 0.03
    expect(b.totalInclusive).toBe(96);
    expect(b.totalInclusive).toBe(u3.buy!.totalInclusive);
    expect(b.marginInclusive).toBeUndefined();
  });

  it("accountant/admin see the SELL breakdown + margin", () => {
    const u3 = computeUsageBilling(RESALE, 6200, "2026-06");
    for (const viewer of ["admin", "accountant"] as const) {
      const b = buildUsageInvoiceBreakdown(RESALE, 6200, "2026-06", { viewer, activatedAt: "2026-06-01" });
      expect(b.legKind).toBe("sell");
      expect(b.totalInclusive).toBe(160);
      expect(b.marginInclusive).toBe(u3.marginInclusive); // 160 − 96 = 64
      expect(b.marginInclusive).toBe(64);
    }
  });

  it("subscription shows the fee + payer; no buy/margin for any viewer", () => {
    for (const viewer of ["merchant", "partner", "admin"] as const) {
      const b = buildUsageInvoiceBreakdown(SUBSCRIPTION, 4800, "2026-06", { viewer, activatedAt: "2026-06-01" });
      expect(b.legKind).toBe("fee");
      expect(b.totalInclusive).toBe(149); // 4800 < 5000 included → base fee only
      expect(b.overageExcess).toBe(0);
      expect(b.payer).toBe("Partner");
      expect(b.marginInclusive).toBeUndefined();
    }
  });

  it("mid-cycle: base prorated by days, overage counted to deactivation", () => {
    // Active 16 Jun (15/30 days): base 100 → 50; overage NOT prorated → 60; total 110.
    const b = buildUsageInvoiceBreakdown(RESALE, 6200, "2026-06", { viewer: "merchant", activatedAt: "2026-06-16" });
    const u3 = computeUsageBilling(RESALE, 6200, "2026-06", 15);

    expect(b.prorated).toBe(true);
    expect(b.activeDays).toBe(15);
    expect(b.baseInclusive).toBe(50);
    expect(b.overageInclusive).toBe(60);
    expect(b.totalInclusive).toBe(110);
    expect(b.totalInclusive).toBe(u3.sell!.totalInclusive);
  });
});
