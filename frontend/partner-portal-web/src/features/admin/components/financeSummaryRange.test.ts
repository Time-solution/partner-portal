import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { drillShowingLabel } from "@/lib/ui/drillContext";
import { scopeReportEntries } from "@/lib/reports/reportScope";
import { createSeedData } from "@/lib/data/fixtures";
import { partnerStatement, merchantStatement, partnersIn, merchantsIn } from "@/lib/reports/settlementReports";

const read = (rel: string) => readFileSync(rel, "utf8");
const data = createSeedData();
const refs = (entries: { orderRef: string }[]) => new Set(entries.map((e) => e.orderRef));

const LABELS = { allDates: "All dates", latestPeriodNote: "latest period" };

describe("Finance summary by company — header 'Showing data for' reflects the range", () => {
  it("a from/to range shows the exact window (not a stray single period)", () => {
    expect(drillShowingLabel({ mode: "range", from: "2026-06-05", to: "2026-06-28" }, "2026-07", LABELS)).toBe(
      "2026-06-05 → 2026-06-28",
    );
  });

  it("a month/day pick reflects that selection", () => {
    expect(drillShowingLabel({ mode: "period", period: "2026-06" }, "2026-07", LABELS)).toBe("2026-06");
    expect(drillShowingLabel({ mode: "day", day: "2026-06-20" }, "2026-07", LABELS)).toBe("2026-06-20");
  });

  it("'All dates' is honest — says it shows the latest period, never a bare stray period", () => {
    const label = drillShowingLabel({ mode: "all" }, "2026-07", LABELS);
    expect(label).toBe("All dates · latest period 2026-07");
    // It must NOT masquerade as a single chosen period like the old "2026-07".
    expect(label).not.toBe("2026-07");
  });
});

describe("Finance summary by company — cards honor the chosen range (day-to-day)", () => {
  // Fixture settlement createdAt: 7001=06-18, 7002=06-10, wthere=06-20, 7003=07-05.
  it("a 2026-06-05..2026-06-28 range narrows company cards to in-range cases; 06-30/07-05 excluded", () => {
    const { entries } = scopeReportEntries(data, { mode: "range", from: "2026-06-05", to: "2026-06-28" });
    const r = refs(entries);
    expect(r.has("order:ord-burger-7001:v1")).toBe(true); // 06-18 in window
    expect(r.has("order:ord-burger-wthere:v1")).toBe(true); // 06-20 in window
    expect(r.has("order:ord-burger-7003:v1")).toBe(false); // 07-05 outside

    // The per-company cards read the SAME day-scoped entries → an in-range partner/merchant appears.
    const partner = partnersIn(entries)[0];
    expect(partner).toBeDefined();
    const ps = partnerStatement(entries, partner.partnerId, "all");
    expect(ps.orderCount).toBeGreaterThan(0);
    const merchant = merchantsIn(entries)[0];
    const ms = merchantStatement(entries, merchant.tenantId, "all");
    expect(ms.orderCount).toBeGreaterThan(0);
  });

  it("a multi-month range aggregates both months on the cards (no month dropped)", () => {
    const both = scopeReportEntries(data, { mode: "range", from: "2026-06-01", to: "2026-07-31" }).entries;
    const r = refs(both);
    expect(r.has("order:ord-burger-7001:v1")).toBe(true); // June
    expect(r.has("order:ord-burger-7003:v1")).toBe(true); // July — present, not collapsed
  });
});

describe("Finance summary by company — wiring (reuses the shared filter, VAT stays period)", () => {
  const src = read("src/features/admin/components/FinanceDrillDown.tsx");

  it("exposes the shared <DateRangeFilter/> (the from/to range picker), not a bespoke control", () => {
    expect(src).toContain("<DateRangeFilter");
    expect(src).toContain("useDateRange");
  });

  it("cards read the day-scoped entries from scopeReportEntries", () => {
    expect(src).toContain("scopeReportEntries");
    expect(src).toContain("dayEntries");
  });

  it("Net VAT stays the period figure (separate vatEntries) and keeps the period note label", () => {
    expect(src).toContain("vatEntries");
    expect(src).toContain("vatControl(vatEntries)");
    expect(src).toContain("reportsNetVatPeriodNote");
  });
});
