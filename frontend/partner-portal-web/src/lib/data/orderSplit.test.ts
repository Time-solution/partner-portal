import { describe, expect, it } from "vitest";
import {
  assertSplitBalances,
  deriveFourWaySplit,
  splitComponentsTotal,
  type FourWaySplit,
  type Money,
} from "./types";

const sar = (amount: number): Money => ({ amount, currency: "SAR", vatInclusive: true });

/**
 * The order split must go FOUR ways (3 parties + tax). The previous 3-way split
 * (Merchant + Delivery + Zahy) was short by the net VAT — the ZATCA recipient was missing.
 *
 *   Collected = Merchant + Delivery + ZahyMargin + NetVAT
 */
describe("four-way split invariant — Collected = Merchant + Delivery + ZahyMargin + NetVAT", () => {
  it("order 901 (COD delivery): 113.00 = 100.00 + 10.00 + 2.60 + 0.40", () => {
    const split: FourWaySplit = {
      merchant: sar(100),
      deliveryCompany: sar(10),
      zahy: sar(2.6),
      zatca: sar(0.4), // NetVAT = Output 1.70 − Input 1.30
    };
    expect(splitComponentsTotal(split)).toBe(113);
    expect(() => assertSplitBalances(sar(113), split)).not.toThrow();
    expect(split.zatca.amount).toBe(0.4);
  });

  it("derives order 901 from the real flow (items 100, delivery buy 10, collected 113)", () => {
    const split = deriveFourWaySplit({ collected: 113, merchantItems: 100, partnerBuyGross: 10 });
    expect(split.merchant.amount).toBe(100);
    expect(split.deliveryCompany.amount).toBe(10);
    expect(split.zahy.amount).toBe(2.6);
    expect(split.zatca.amount).toBe(0.4);
    expect(splitComponentsTotal(split)).toBe(113);
  });

  it("canonical Principal resale 70→100 (both VAT-inclusive): 100.00 = 0 + 70.00 + 26.09 + 3.91", () => {
    // collected 100 (sell, VAT-incl) ; partner buy 70 (VAT-incl) ; no merchant items, no delivery.
    // Output VAT 13.04, Input VAT 9.13 → Net VAT 3.91 ; margin 26.09.
    const split = deriveFourWaySplit({ collected: 100, merchantItems: 0, partnerBuyGross: 70 });
    expect(split.merchant.amount).toBe(0);
    expect(split.deliveryCompany.amount).toBe(70);
    expect(split.zahy.amount).toBe(26.09);
    expect(split.zatca.amount).toBe(3.91);
    expect(splitComponentsTotal(split)).toBe(100);
    expect(() => assertSplitBalances(sar(100), split)).not.toThrow();
  });

  it("REJECTS the old three-way split that is short by the net VAT", () => {
    // Old split forgot ZATCA (zatca 0): 100 + 10 + 2.60 + 0 = 112.60 ≠ 113.00 collected.
    const shortSplit: FourWaySplit = {
      merchant: sar(100),
      deliveryCompany: sar(10),
      zahy: sar(2.6),
      zatca: sar(0),
    };
    expect(splitComponentsTotal(shortSplit)).toBe(112.6);
    expect(() => assertSplitBalances(sar(113), shortSplit)).toThrow(/imbalance/i);
  });
});
