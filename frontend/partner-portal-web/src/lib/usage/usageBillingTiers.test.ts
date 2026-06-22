import { describe, expect, it } from "vitest";
import { computeUsageBilling } from "./usageBilling";
import { scopeUsagePackage, type UsagePackage } from "./usagePackage";
import { buildUsageInvoiceBreakdown } from "./usageInvoiceBreakdown";

const base = {
  partnerId: "22222222-2222-2222-2222-222222222004",
  unitLabel: "messages",
  currency: "SAR",
  payer: "Merchant" as const,
  status: "Published" as const,
};

// included 5,000; tiers on overage units: 0–10,000 then 10,000+ (open).
const RESALE_TIERED: UsagePackage = {
  ...base,
  id: "pkg-resale-tiered",
  name: "Resale tiered",
  mode: "Resale",
  includedQuantity: 5000,
  baseBuyAmount: 0,
  baseSellAmount: 0,
  overageBuyAmount: 0.99, // flat — must be IGNORED when tiers exist
  overageSellAmount: 0.99,
  tiers: [
    { fromQuantity: 0, toQuantity: 10000, buyRate: 0.03, sellRate: 0.05 },
    { fromQuantity: 10000, toQuantity: null, buyRate: 0.025, sellRate: 0.04 },
  ],
};

const RESALE_FLAT: UsagePackage = {
  ...base,
  id: "pkg-resale-flat",
  name: "Resale flat",
  mode: "Resale",
  includedQuantity: 5000,
  baseBuyAmount: 0,
  baseSellAmount: 0,
  overageBuyAmount: 0.03,
  overageSellAmount: 0.05,
};

const SUB_TIERED: UsagePackage = {
  ...base,
  id: "pkg-sub-tiered",
  name: "Sub tiered",
  mode: "Subscription",
  includedQuantity: 5000,
  baseBuyAmount: 0,
  baseSellAmount: 0,
  overageBuyAmount: 0,
  overageSellAmount: 0.99,
  payer: "Partner",
  tiers: [
    { fromQuantity: 0, toQuantity: 10000, buyRate: 0, sellRate: 0.05 },
    { fromQuantity: 10000, toQuantity: null, buyRate: 0, sellRate: 0.04 },
  ],
};

describe("U5 graduated volume tiers — billing calc", () => {
  it("no tiers = identical to flat U3", () => {
    const r = computeUsageBilling(RESALE_FLAT, 16200, "2026-06"); // overage 11,200
    expect(r.sell!.overageInclusive).toBe(560); // 11,200 × 0.05
    expect(r.sell!.tiers.length).toBe(0);
  });

  it("graduated: first 10,000 × 0.05 = 500 + next 1,200 × 0.04 = 48 -> 548", () => {
    const r = computeUsageBilling(RESALE_TIERED, 16200, "2026-06"); // overage 11,200

    expect(r.sell!.overageInclusive).toBe(548);
    expect(r.sell!.tiers.length).toBe(2);
    expect(r.sell!.tiers[0].units).toBe(10000);
    expect(r.sell!.tiers[0].amountInclusive).toBe(500);
    expect(r.sell!.tiers[1].units).toBe(1200); // open-ended top tier
    expect(r.sell!.tiers[1].amountInclusive).toBe(48);

    // Buy side: 300 + 30 = 330; margin = 548 − 330 = 218.
    expect(r.buy!.overageInclusive).toBe(330);
    expect(r.marginInclusive).toBe(218);

    // Routes to the existing Principal template; trial balance nets to zero (compute-only).
    expect(r.template).toBe("Principal");
  });

  it("usage within the first tier only charges that bracket", () => {
    const r = computeUsageBilling(RESALE_TIERED, 11000, "2026-06"); // overage 6,000
    expect(r.sell!.overageInclusive).toBe(300); // 6,000 × 0.05
    expect(r.sell!.tiers.length).toBe(1);
  });

  it("subscription tiers use the fee rate and route to Fee", () => {
    const r = computeUsageBilling(SUB_TIERED, 16200, "2026-06");
    expect(r.fee!.overageInclusive).toBe(548);
    expect(r.fee!.tiers.length).toBe(2);
    expect(r.template).toBe("Fee");
    expect(r.payer).toBe("Partner");
  });
});

describe("U5 graduated volume tiers — scope + breakdown", () => {
  it("scopeUsagePackage scopes tier rates: merchant sell, partner buy, margin admin-only", () => {
    const merchant = scopeUsagePackage(RESALE_TIERED, "merchant");
    expect(merchant.tiers[0].sell?.amount).toBe(0.05);
    expect(merchant.tiers[0].buy).toBeUndefined();
    expect(merchant.tiers[0].margin).toBeUndefined();

    const partner = scopeUsagePackage(RESALE_TIERED, "partner");
    expect(partner.tiers[0].buy?.amount).toBe(0.03);
    expect(partner.tiers[0].sell).toBeUndefined();

    const admin = scopeUsagePackage(RESALE_TIERED, "admin");
    expect(admin.tiers[0].margin?.amount).toBeCloseTo(0.02, 10);
  });

  it("breakdown shows per-tier detail scoped to the viewer's leg", () => {
    const merchant = buildUsageInvoiceBreakdown(RESALE_TIERED, 16200, "2026-06", { viewer: "merchant", activatedAt: "2026-06-01" });
    expect(merchant.legKind).toBe("sell");
    expect(merchant.tiers.map((x) => x.amountInclusive)).toEqual([500, 48]);
    expect(merchant.overageInclusive).toBe(548);
    expect(merchant.marginInclusive).toBeUndefined();

    const partner = buildUsageInvoiceBreakdown(RESALE_TIERED, 16200, "2026-06", { viewer: "partner", activatedAt: "2026-06-01" });
    expect(partner.legKind).toBe("buy");
    expect(partner.tiers.map((x) => x.amountInclusive)).toEqual([300, 30]);
    expect(partner.marginInclusive).toBeUndefined();

    const admin = buildUsageInvoiceBreakdown(RESALE_TIERED, 16200, "2026-06", { viewer: "admin", activatedAt: "2026-06-01" });
    expect(admin.marginInclusive).toBe(218);
  });

  it("flat package breakdown is unchanged (no tier lines)", () => {
    const b = buildUsageInvoiceBreakdown(RESALE_FLAT, 16200, "2026-06", { viewer: "merchant", activatedAt: "2026-06-01" });
    expect(b.tiers.length).toBe(0);
    expect(b.overageInclusive).toBe(560);
  });
});
