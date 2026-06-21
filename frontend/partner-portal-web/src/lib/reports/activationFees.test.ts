import { describe, expect, it } from "vitest";
import type { ReflectedPartnerOrder } from "@/lib/data/types";
import { buildPartnerBulkInvoice, feeBreakdown } from "./activationFees";

describe("feeBreakdown", () => {
  it("splits 1.00 incl into 0.87 ex-VAT + 0.13 VAT", () => {
    expect(feeBreakdown(1)).toEqual({ inclusive: 1, net: 0.87, vat: 0.13 });
  });
  it("splits 40.00 incl into 34.78 + 5.22", () => {
    expect(feeBreakdown(40)).toEqual({ inclusive: 40, net: 34.78, vat: 5.22 });
  });
});

const sar = (amount: number) => ({ amount, currency: "SAR", vatInclusive: true });

function order(id: string, tenant: string, merchant: string, at: string): ReflectedPartnerOrder {
  return {
    id,
    partnerId: "chefz",
    partnerName: "Chefz",
    tenantId: tenant,
    merchantName: merchant,
    externalTransactionId: `order:${id}:v1`,
    orderLineId: `${id}-l1`,
    posSyncStatus: "Reflected",
    reflectedAt: at,
    reflection: {
      merchantMenuPrice: sar(10),
      partnerListPrice: sar(13),
      deliveryFee: sar(10),
      customerPaid: sar(23),
      zahyFeeInclusive: sar(1),
      successful: true,
    },
  };
}

describe("buildPartnerBulkInvoice", () => {
  const orders = [
    order("a", "t1", "Shawarma Time", "2026-06-03T00:00:00Z"),
    order("b", "t1", "Shawarma Time", "2026-06-11T00:00:00Z"),
    order("c", "t2", "Pasta Corner", "2026-06-14T00:00:00Z"),
    order("d", "t2", "Pasta Corner", "2026-07-02T00:00:00Z"), // other period — excluded
  ];

  it("sums successful txns across all merchants for the period", () => {
    const inv = buildPartnerBulkInvoice(orders, "chefz", "2026-06");
    expect(inv.transactionCount).toBe(3);
    expect(inv.totalInclusive).toBe(3);
    expect(inv.totalNet).toBe(2.61);
    expect(inv.totalVat).toBe(0.39);
    expect(inv.lines.map((l) => l.merchantName)).toEqual(["Pasta Corner", "Shawarma Time"]);
    expect(inv.lines.find((l) => l.tenantId === "t1")?.transactions.length).toBe(2);
  });

  it("excludes unsuccessful transactions", () => {
    const withFailed = [
      ...orders.slice(0, 2),
      { ...order("e", "t1", "Shawarma Time", "2026-06-20T00:00:00Z"), reflection: { ...order("e", "t1", "x", "2026-06-20T00:00:00Z").reflection!, successful: false } },
    ];
    const inv = buildPartnerBulkInvoice(withFailed, "chefz", "2026-06");
    expect(inv.transactionCount).toBe(2);
  });
});
