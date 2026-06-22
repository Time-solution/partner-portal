import { describe, expect, it } from "vitest";
import { createSeedData } from "@/lib/data/fixtures";
import { listPeriods, platformTotals, toReportEntries, vatControl } from "@/lib/reports/settlementReports";
import { scopeReportEntries } from "./reportScope";

const data = createSeedData();
const r2 = (n: number) => Math.round(n * 100) / 100;
const refs = (entries: { orderRef: string }[]) => new Set(entries.map((e) => e.orderRef));
const subRefs = (entries: { orderRef: string; mode: string }[]) =>
  new Set(entries.filter((e) => e.mode === "SubscriptionFee").map((e) => e.orderRef));

// Fixture settlement dates (createdAt): 7001=06-18, 7002=06-10, wthere-svc=06-20, 7003=07-05.

describe("scopeReportEntries — SALES/PURCHASE day-to-day (engine math untouched)", () => {
  it("day-precise range 06-05..06-19 includes in-window cases, excludes the 06-20 case", () => {
    const { entries } = scopeReportEntries(data, { mode: "range", from: "2026-06-05", to: "2026-06-19" });
    const r = refs(entries);
    expect(r.has("order:ord-burger-7001:v1")).toBe(true); // 06-18 in
    expect(r.has("order:ord-burger-7002:v1")).toBe(true); // 06-10 in
    expect(r.has("order:ord-burger-wthere:v1")).toBe(false); // 06-20 excluded (day-precise)
    expect(r.has("order:ord-burger-7003:v1")).toBe(false); // 07-05 excluded
  });

  it("single-day mode picks only that day's cases", () => {
    const { entries } = scopeReportEntries(data, { mode: "day", day: "2026-06-20" });
    const r = refs(entries);
    expect(r.has("order:ord-burger-wthere:v1")).toBe(true); // 06-20
    expect(r.has("order:ord-burger-7001:v1")).toBe(false); // 06-18 not this day
  });

  it("multi-month range AGGREGATES all in-range cases — no single-month collapse (old rangeMonth bug)", () => {
    const both = scopeReportEntries(data, { mode: "range", from: "2026-06-01", to: "2026-07-31" });
    const r = refs(both.entries);
    expect(r.has("order:ord-burger-7001:v1")).toBe(true); // June present
    expect(r.has("order:ord-burger-7003:v1")).toBe(true); // July present — NOT dropped

    // Aggregate property: the multi-month figure equals the SUM of the per-month figures
    // (no month dropped, no double count) — proving real cross-month aggregation.
    const june = platformTotals(scopeReportEntries(data, { mode: "period", period: "2026-06" }).entries, "all");
    const july = platformTotals(scopeReportEntries(data, { mode: "period", period: "2026-07" }).entries, "all");
    const agg = platformTotals(both.entries, "all");
    expect(r2(agg.resaleMargin)).toBe(r2(june.resaleMargin + july.resaleMargin));
    expect(r2(agg.feeRevenue)).toBe(r2(june.feeRevenue + july.feeRevenue));
    expect(agg.orderCount).toBe(june.orderCount + july.orderCount);
  });

  it("never widens — every scoped entry is a subset of the full entry set (scope preserved)", () => {
    const full = new Set(toReportEntries(data).map((e) => `${e.orderRef}|${e.mode}`));
    const { entries } = scopeReportEntries(data, { mode: "range", from: "2026-06-01", to: "2026-06-30" });
    for (const e of entries) expect(full.has(`${e.orderRef}|${e.mode}`)).toBe(true);
  });

  it("untouched 'all' collapses to the latest period — no behaviour change when not filtering", () => {
    const latest = listPeriods(toReportEntries(data))[0];
    const view = scopeReportEntries(data, { mode: "all" }, latest);
    expect(view.effectiveRange).toEqual({ mode: "period", period: latest });
    for (const e of view.entries) expect(e.period).toBe(latest);
    expect(view.dayGrain).toBe(false);
  });
});

describe("scopeReportEntries — VAT STAYS period/month-based (never day-sliced)", () => {
  it("a single-day pick shows the WHOLE touched month's net VAT (not a day slice)", () => {
    const dayPick = scopeReportEntries(data, { mode: "day", day: "2026-06-20" });
    const monthPick = scopeReportEntries(data, { mode: "period", period: "2026-06" });
    expect(vatControl(dayPick.vatEntries).netVatToZatca).toBe(
      vatControl(monthPick.vatEntries).netVatToZatca,
    );
    // ...while the SALES/PURCHASE entries DID narrow to the day (fewer than the whole month).
    expect(dayPick.entries.length).toBeLessThan(monthPick.entries.length);
    expect(dayPick.dayGrain).toBe(true);
  });

  it("a multi-month range's net VAT sums the touched filing periods (month-grained)", () => {
    const june = vatControl(scopeReportEntries(data, { mode: "period", period: "2026-06" }).vatEntries);
    const july = vatControl(scopeReportEntries(data, { mode: "period", period: "2026-07" }).vatEntries);
    const both = vatControl(
      scopeReportEntries(data, { mode: "range", from: "2026-06-01", to: "2026-07-31" }).vatEntries,
    );
    expect(r2(both.netVatToZatca)).toBe(r2(june.netVatToZatca + july.netVatToZatca));
  });
});

describe("scopeReportEntries — subscription fees month-overlap (cannot day-split)", () => {
  it("same SubscriptionFee set for ANY day window inside the month; absent for another month", () => {
    const early = scopeReportEntries(data, { mode: "range", from: "2026-06-01", to: "2026-06-02" });
    const late = scopeReportEntries(data, { mode: "range", from: "2026-06-26", to: "2026-06-27" });
    const earlySubs = subRefs(early.entries);
    const lateSubs = subRefs(late.entries);
    expect(earlySubs.size).toBeGreaterThan(0);
    // identical whole-month subscription set regardless of which June day is picked (no day-split)
    expect([...earlySubs].sort()).toEqual([...lateSubs].sort());

    // a window in a different month does not pull June's subscription fees
    const may = scopeReportEntries(data, { mode: "range", from: "2026-05-01", to: "2026-05-10" });
    expect(subRefs(may.entries).size).toBe(0);
  });
});
