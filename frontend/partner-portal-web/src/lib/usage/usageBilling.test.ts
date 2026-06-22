import { describe, expect, it } from "vitest";
import {
  billingTrialBalanceNet,
  computeUsageBilling,
  daysInPeriod,
  scopeUsageBilling,
} from "./usageBilling";
import type { UsagePackage } from "./usagePackage";

const JUNE = "2026-06"; // 30 days

function resale(p: Partial<UsagePackage> = {}): UsagePackage {
  return {
    id: "pkg-resale",
    partnerId: "ptr-1",
    name: "Resale",
    unitLabel: "messages",
    mode: "Resale",
    currency: "SAR",
    includedQuantity: 10000,
    baseBuyAmount: 200,
    baseSellAmount: 300,
    overageBuyAmount: 0.02,
    overageSellAmount: 0.05,
    payer: "Merchant",
    status: "Published",
    ...p,
  };
}

function subscription(p: Partial<UsagePackage> = {}): UsagePackage {
  return {
    id: "pkg-sub",
    partnerId: "ptr-1",
    name: "Sub",
    unitLabel: "messages",
    mode: "Subscription",
    currency: "SAR",
    includedQuantity: 5000,
    baseBuyAmount: 0,
    baseSellAmount: 149,
    overageBuyAmount: 0,
    overageSellAmount: 0.03,
    payer: "Merchant",
    status: "Published",
    ...p,
  };
}

describe("computeUsageBilling — amount formula", () => {
  it("charges base only when usage is under the included quantity", () => {
    const r = computeUsageBilling(subscription(), 3000, JUNE);
    expect(r.fee?.overageExcess).toBe(0);
    expect(r.fee?.overageInclusive).toBe(0);
    expect(r.fee?.totalInclusive).toBe(149);
  });

  it("adds overage × excess when usage exceeds included", () => {
    const r = computeUsageBilling(subscription(), 6200, JUNE);
    expect(r.fee?.overageExcess).toBe(1200);
    expect(r.fee?.overageInclusive).toBe(36); // 1200 × 0.03
    expect(r.fee?.totalInclusive).toBe(185); // 149 + 36
  });

  it("pure per-use is rate × usage (base 0, included 0)", () => {
    const r = computeUsageBilling(
      subscription({ includedQuantity: 0, baseSellAmount: 0, overageSellAmount: 0.05 }),
      1000,
      JUNE,
    );
    expect(r.fee?.totalInclusive).toBe(50);
  });
});

describe("computeUsageBilling — resale routes to Principal", () => {
  it("computes buy + sell + margin and a balanced Principal journal", () => {
    const r = computeUsageBilling(resale(), 12000, JUNE); // excess 2000
    expect(r.buy?.totalInclusive).toBe(240); // 200 + 2000×0.02
    expect(r.sell?.totalInclusive).toBe(400); // 300 + 2000×0.05
    expect(r.marginInclusive).toBe(160); // 400 − 240
    expect(r.template).toBe("Principal");
    expect(r.lines).toHaveLength(6);
    expect(billingTrialBalanceNet(r)).toBe(0);
    expect(r.lines.find((l) => l.account === "1200" && l.direction === "Debit")?.amount).toBe(400);
    expect(r.lines.find((l) => l.account === "2100" && l.direction === "Credit")?.amount).toBe(240);
  });
});

describe("computeUsageBilling — subscription routes to Fee", () => {
  it("merchant payer debits 1200; partner payer debits 1250; balanced", () => {
    const merchant = computeUsageBilling(subscription(), 6200, JUNE);
    expect(merchant.template).toBe("Fee");
    expect(merchant.payer).toBe("Merchant");
    expect(merchant.lines.find((l) => l.account === "1200" && l.direction === "Debit")?.amount).toBe(185);
    expect(billingTrialBalanceNet(merchant)).toBe(0);

    const partner = computeUsageBilling(subscription({ payer: "Partner" }), 6200, JUNE);
    expect(partner.payer).toBe("Partner");
    expect(partner.lines.find((l) => l.account === "1250" && l.direction === "Debit")?.amount).toBe(185);
    expect(billingTrialBalanceNet(partner)).toBe(0);
  });
});

describe("proration + counted-to-deactivation", () => {
  it("prorates the base by calendar days; usage (overage) is NOT prorated", () => {
    const r = computeUsageBilling(
      subscription({ baseSellAmount: 300, overageSellAmount: 0.1 }),
      6000,
      JUNE,
      15,
    );
    expect(r.fee?.baseInclusive).toBe(150); // 300 × 15/30
    expect(r.fee?.overageExcess).toBe(1000); // 6000 − 5000, full units to deactivation
    expect(r.fee?.overageInclusive).toBe(100);
    expect(r.fee?.totalInclusive).toBe(250);
  });

  it("daysInPeriod reads the month length (June = 30, Feb 2026 = 28)", () => {
    expect(daysInPeriod("2026-06")).toBe(30);
    expect(daysInPeriod("2026-02")).toBe(28);
  });
});

describe("VAT split via the single helper, reconciles", () => {
  it("splits 115 incl → net 100 + VAT 15 and the journal balances", () => {
    const r = computeUsageBilling(
      subscription({ includedQuantity: 0, baseSellAmount: 115, overageSellAmount: 0 }),
      0,
      JUNE,
    );
    expect(r.lines.find((l) => l.account === "4200")?.amount).toBe(100);
    expect(r.lines.find((l) => l.account === "2200")?.amount).toBe(15);
    expect(billingTrialBalanceNet(r)).toBe(0);
  });
});

describe("scopeUsageBilling — structural visibility (reuses U2 rule)", () => {
  const result = computeUsageBilling(resale(), 12000, JUNE);

  it("admin/accountant see buy + sell + margin", () => {
    const admin = scopeUsageBilling(result, "admin");
    expect(admin.buy?.amount).toBe(240);
    expect(admin.sell?.amount).toBe(400);
    expect(admin.margin?.amount).toBe(160);
  });

  it("partner sees BUY only — sell and margin absent", () => {
    const partner = scopeUsageBilling(result, "partner");
    expect(partner.buy?.amount).toBe(240);
    expect(partner.sell).toBeUndefined();
    expect(partner.margin).toBeUndefined();
  });

  it("merchant sees SELL only — buy and margin absent", () => {
    const merchant = scopeUsageBilling(result, "merchant");
    expect(merchant.sell?.amount).toBe(400);
    expect(merchant.buy).toBeUndefined();
    expect(merchant.margin).toBeUndefined();
  });

  it("subscription shows fee + payer only — no buy/sell/margin", () => {
    const fee = scopeUsageBilling(computeUsageBilling(subscription(), 6200, JUNE), "merchant");
    expect(fee.fee?.amount).toBe(185);
    expect(fee.payer).toBe("Merchant");
    expect(fee.buy).toBeUndefined();
    expect(fee.sell).toBeUndefined();
    expect(fee.margin).toBeUndefined();
  });
});

describe("compute-only", () => {
  it("rejects negative usage and never posts (journal nets zero)", () => {
    expect(() => computeUsageBilling(subscription(), -1, JUNE)).toThrow();
    expect(billingTrialBalanceNet(computeUsageBilling(resale(), 12000, JUNE))).toBe(0);
  });
});
