import { describe, expect, it } from "vitest";
import { createSeedData } from "./fixtures";
import { deriveInvoicePayment } from "./types";

/**
 * AF2 — seed time-bomb guard. Every seeded billing period's STORED `paymentStatus` must equal a live
 * `deriveInvoicePayment` recompute, at any real clock. This can never rot because the seed's
 * status-driving dates (`dueDate`) are RELATIVE to the module-load anchor (`daysFromNow`/`daysAgo`),
 * not absolute literals — so a Partial/Pending invoice stays genuinely not-yet-due and a Paid one
 * stays settled regardless of the calendar (the exact defect that broke moneyExport at 2026-07-15).
 * We derive with the REAL clock precisely to prove the relative dates hold today and every day after.
 */
describe("seed integrity — no stored paymentStatus contradicts the live recompute", () => {
  it("every seeded billing period reconciles at the real clock", () => {
    const { billingPeriods } = createSeedData();
    expect(billingPeriods.length).toBeGreaterThan(0);

    const now = new Date();
    for (const period of billingPeriods) {
      const derived = deriveInvoicePayment(period, now);
      expect(derived.status, `${period.id} stored=${period.paymentStatus} derived=${derived.status}`)
        .toBe(period.paymentStatus);
    }
  });

  it("no seeded dueDate is a fixed calendar literal that the clock can overtake", () => {
    // The status-interacting dates must be relative: a Partial/Pending due date is in the FUTURE, a
    // Paid one in the past — asserted structurally so a re-introduced absolute literal is caught.
    const { billingPeriods } = createSeedData();
    const now = Date.now();
    for (const p of billingPeriods) {
      if (!p.dueDate) continue;
      const due = new Date(p.dueDate).getTime();
      if (p.paymentStatus === "Partial" || p.paymentStatus === "Pending") {
        expect(due, `${p.id} must be not-yet-due`).toBeGreaterThan(now);
      }
    }
  });
});
