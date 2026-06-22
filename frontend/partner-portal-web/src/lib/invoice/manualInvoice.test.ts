import { describe, expect, it } from "vitest";
import {
  computeManualInvoiceLines,
  computeManualInvoiceTotals,
  isManualInvoiceValid,
  previewManualInvoice,
  validateManualInvoiceLines,
} from "./manualInvoice";
import type { ManualInvoiceLineInput } from "@/lib/data/types";

/** The LOCKED worked example — identical to the backend ManualInvoiceTests fixture. */
const WORKED_EXAMPLE: ManualInvoiceLineInput[] = [
  { description: "Foo", quantity: 1, unitPriceInclusive: 100 },
  { description: "Bar", quantity: 2, unitPriceInclusive: 50 },
  { description: "Baz", quantity: 1, unitPriceInclusive: 200 },
];

describe("manualInvoice — locked inclusive back-out (matches backend)", () => {
  it("worked example: 3 lines → net 347.83 / VAT 52.17 / total 400.00", () => {
    const { lines, totals } = previewManualInvoice(WORKED_EXAMPLE);

    // Per-line VAT split (round-per-line): net + vat === line total.
    for (const line of lines) {
      expect(line.vatNet + line.vatAmount).toBeCloseTo(line.lineTotalInclusive, 2);
    }
    // Bar is qty 2 × 50 = 100 inclusive (quantity is multiplied, not summed wrong).
    expect(lines[1].lineTotalInclusive).toBe(100);

    expect(totals.subtotalNet).toBe(347.83);
    expect(totals.vatTotal).toBe(52.17);
    expect(totals.grandTotalInclusive).toBe(400);

    // Sanity: re-summing the computed lines yields the same totals.
    const resummed = computeManualInvoiceTotals(computeManualInvoiceLines(WORKED_EXAMPLE));
    expect(resummed).toEqual(totals);
  });

  it("blocks empty lines (mirrors backend Zahy.Finance:081)", () => {
    expect(isManualInvoiceValid([])).toBe(false);
    expect(validateManualInvoiceLines([])).toEqual([{ index: -1, field: "lines" }]);
  });

  it("enforces qty > 0 and unit price > 0 (zero/negative price and zero qty rejected)", () => {
    const zeroQty: ManualInvoiceLineInput[] = [{ description: "Z", quantity: 0, unitPriceInclusive: 100 }];
    expect(isManualInvoiceValid(zeroQty)).toBe(false);
    expect(validateManualInvoiceLines(zeroQty).some((e) => e.field === "quantity")).toBe(true);

    const negativePrice: ManualInvoiceLineInput[] = [{ description: "Z", quantity: 1, unitPriceInclusive: -1 }];
    expect(isManualInvoiceValid(negativePrice)).toBe(false);
    expect(validateManualInvoiceLines(negativePrice).some((e) => e.field === "unitPriceInclusive")).toBe(true);

    // Zero price is now rejected — matches the backend domain rule (reject ≤ 0).
    const zeroPrice: ManualInvoiceLineInput[] = [{ description: "Z", quantity: 1, unitPriceInclusive: 0 }];
    expect(isManualInvoiceValid(zeroPrice)).toBe(false);
    expect(validateManualInvoiceLines(zeroPrice).some((e) => e.field === "unitPriceInclusive")).toBe(true);

    // A positive qty and positive price is valid.
    expect(isManualInvoiceValid([{ description: "Z", quantity: 1, unitPriceInclusive: 100 }])).toBe(true);
  });
});
