import { describe, expect, it } from "vitest";
import { splitInclusiveVat } from "@/lib/reports/settlementReports";
import {
  rollupPartnerPayableVat,
  splitPartnerPayableInclusive,
} from "./partnerPayableVat";

describe("splitPartnerPayableInclusive", () => {
  it("splits 70 incl via splitInclusiveVat — never VAT 0 on VAT-inclusive payable", () => {
    const split = splitPartnerPayableInclusive(70);
    expect(splitInclusiveVat(70)).toEqual({ exVat: split.exVat, vat: split.inputVat });
    expect(split).toEqual({ exVat: 60.87, inputVat: 9.13, inclusive: 70 });
    expect(split.inputVat).not.toBe(0);
  });

  it("returns zeros for zero payable", () => {
    expect(splitPartnerPayableInclusive(0)).toEqual({ exVat: 0, inputVat: 0, inclusive: 0 });
  });

  it("rollup sums line splits and keeps read-model grand total", () => {
    const lines = [splitPartnerPayableInclusive(10), splitPartnerPayableInclusive(70)];
    const rolled = rollupPartnerPayableVat(lines, 80);
    expect(rolled.inclusive).toBe(80);
    expect(rolled.exVat).toBe(69.57);
    expect(rolled.inputVat).toBe(10.43);
  });
});
