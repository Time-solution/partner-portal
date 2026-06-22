import { describe, expect, it } from "vitest";
import {
  ALL_DATES,
  applyDateRangeToParams,
  currentPeriod,
  dateRangeFromParams,
  defaultRangeBounds,
  filterByDate,
  inDateRange,
  rangeMonth,
  type DateRange,
} from "./dateRange";

// Day-precise rows (settlement / reflected / activation shapes)
const dayRows = [
  { id: "a", date: "2026-04-15T08:00:00Z" },
  { id: "b", date: "2026-05-20T08:00:00Z" },
  { id: "c", date: "2026-06-10T11:00:00Z" },
  { id: "d", date: "2026-06-20T11:00:00Z" },
  { id: "e", date: "2026-07-05T09:30:00Z" },
];
const getDate = (r: { date: string }) => r.date;

describe("inDateRange — core predicate", () => {
  it('"all" (default) includes everything — no narrowing', () => {
    for (const r of dayRows) expect(inDateRange(r.date, ALL_DATES)).toBe(true);
  });

  it("period mode matches by calendar month", () => {
    const range: DateRange = { mode: "period", period: "2026-06" };
    expect(inDateRange("2026-06-10T11:00:00Z", range)).toBe(true);
    expect(inDateRange("2026-05-20T08:00:00Z", range)).toBe(false);
  });

  it("day mode matches a single calendar day", () => {
    const range: DateRange = { mode: "day", day: "2026-06-20" };
    expect(inDateRange("2026-06-20T11:00:00Z", range)).toBe(true);
    expect(inDateRange("2026-06-10T11:00:00Z", range)).toBe(false);
  });

  it("range mode matches an inclusive [from..to] window", () => {
    const range: DateRange = { mode: "range", from: "2026-05-01", to: "2026-06-15" };
    expect(inDateRange("2026-05-20T08:00:00Z", range)).toBe(true);
    expect(inDateRange("2026-06-10T11:00:00Z", range)).toBe(true);
    expect(inDateRange("2026-06-20T11:00:00Z", range)).toBe(false);
    expect(inDateRange("2026-04-15T08:00:00Z", range)).toBe(false);
  });

  it("month-keyed rows (YYYY-MM) overlap a day/range pick", () => {
    expect(inDateRange("2026-06", { mode: "day", day: "2026-06-20" })).toBe(true);
    expect(inDateRange("2026-06", { mode: "day", day: "2026-07-01" })).toBe(false);
    expect(inDateRange("2026-06", { mode: "range", from: "2026-06-10", to: "2026-06-15" })).toBe(true);
    expect(inDateRange("2026-06", { mode: "range", from: "2026-07-01", to: "2026-07-31" })).toBe(false);
  });

  it("undated rows are never hidden by a date filter", () => {
    expect(inDateRange("", { mode: "day", day: "2026-06-20" })).toBe(true);
    expect(inDateRange(undefined, { mode: "range", from: "2026-06-01" })).toBe(true);
  });
});

describe("filterByDate — narrows a list (range AND single day)", () => {
  it("narrows by a date range", () => {
    const out = filterByDate(dayRows, { mode: "range", from: "2026-05-01", to: "2026-06-15" }, getDate);
    expect(out.map((r) => r.id)).toEqual(["b", "c"]);
  });

  it("narrows by a single day", () => {
    const out = filterByDate(dayRows, { mode: "day", day: "2026-06-20" }, getDate);
    expect(out.map((r) => r.id)).toEqual(["d"]);
  });

  it("narrows by period (existing month filter still works)", () => {
    const out = filterByDate(dayRows, { mode: "period", period: "2026-06" }, getDate);
    expect(out.map((r) => r.id)).toEqual(["c", "d"]);
  });

  it('default ("all") returns the input unchanged — no behavior change when untouched', () => {
    const out = filterByDate(dayRows, ALL_DATES, getDate);
    expect(out).toBe(dayRows); // identity: same reference, nothing filtered
  });

  it("never widens — output is always a subset of the input (RBAC/scope preserved)", () => {
    // Simulate an already-scoped list (e.g. partner sees only their own rows).
    const scoped = dayRows.filter((r) => r.id === "c" || r.id === "d");
    const out = filterByDate(scoped, { mode: "period", period: "2026-06" }, getDate);
    // every result was already in the scoped set — the filter cannot reintroduce hidden rows
    for (const r of out) expect(scoped).toContain(r);
    // a different period can only shrink, never reach outside scope
    const none = filterByDate(scoped, { mode: "period", period: "2026-07" }, getDate);
    expect(none).toEqual([]);
  });
});

describe("filterByDate — each wired tab's accessor", () => {
  it("reflected orders filter by reflectedAt", () => {
    const orders = [
      { id: "o1", reflectedAt: "2026-06-19T11:30:00Z" },
      { id: "o2", reflectedAt: "2026-07-02T11:30:00Z" },
    ];
    const out = filterByDate(orders, { mode: "period", period: "2026-06" }, (o) => o.reflectedAt);
    expect(out.map((o) => o.id)).toEqual(["o1"]);
  });

  it("activations filter by activation.activatedAt (undated pending stays visible)", () => {
    const rows = [
      { activation: { activatedAt: "2026-06-01T09:00:00Z" } },
      { activation: { activatedAt: "2026-05-20T08:00:00Z" } },
      { activation: { activatedAt: undefined as string | undefined } },
    ];
    const out = filterByDate(rows, { mode: "period", period: "2026-06" }, (r) => r.activation.activatedAt);
    expect(out.length).toBe(2); // June row + the undated (pending) row
  });

  it("billing rows filter by periodKey (month-keyed)", () => {
    const rows = [{ periodKey: "2026-06" }, { periodKey: "2026-07" }];
    const out = filterByDate(rows, { mode: "day", day: "2026-06-18" }, (r) => r.periodKey);
    expect(out.map((r) => r.periodKey)).toEqual(["2026-06"]);
  });
});

describe("rangeMonth — month-grained read models (Reports / drill-down)", () => {
  it("derives the representative month, undefined for 'all'", () => {
    expect(rangeMonth({ mode: "period", period: "2026-06" })).toBe("2026-06");
    expect(rangeMonth({ mode: "day", day: "2026-06-20" })).toBe("2026-06");
    expect(rangeMonth({ mode: "range", from: "2026-05-01", to: "2026-06-15" })).toBe("2026-06");
    expect(rangeMonth(ALL_DATES)).toBeUndefined();
  });
});

describe("URL state — single source shared across tabs", () => {
  it("reads day / range / period / all from params", () => {
    expect(dateRangeFromParams(new URLSearchParams("day=2026-06-20"))).toEqual({ mode: "day", day: "2026-06-20" });
    expect(dateRangeFromParams(new URLSearchParams("from=2026-05-01&to=2026-06-15"))).toEqual({
      mode: "range",
      from: "2026-05-01",
      to: "2026-06-15",
    });
    expect(dateRangeFromParams(new URLSearchParams("period=2026-06"))).toEqual({ mode: "period", period: "2026-06" });
    expect(dateRangeFromParams(new URLSearchParams(""))).toEqual({ mode: "all" });
  });

  it("is compatible with the existing Reports ?period= param", () => {
    const r = dateRangeFromParams(new URLSearchParams("period=2026-06&entity=all"));
    expect(r).toEqual({ mode: "period", period: "2026-06" });
  });

  it("round-trips and clears stale date keys when changing mode", () => {
    let p = applyDateRangeToParams(new URLSearchParams("entity=partner:x"), { mode: "range", from: "2026-05-01", to: "2026-06-15" });
    expect(dateRangeFromParams(p)).toEqual({ mode: "range", from: "2026-05-01", to: "2026-06-15" });
    expect(p.get("entity")).toBe("partner:x"); // other params preserved (scope/entity untouched)
    p = applyDateRangeToParams(p, { mode: "period", period: "2026-07" });
    expect(p.get("from")).toBeNull();
    expect(p.get("to")).toBeNull();
    expect(dateRangeFromParams(p)).toEqual({ mode: "period", period: "2026-07" });
  });

  it("persists range mode with default bounds when from/to are missing (mode selector fix)", () => {
    const p = applyDateRangeToParams(new URLSearchParams(""), { mode: "range" });
    const r = dateRangeFromParams(p);
    expect(r.mode).toBe("range");
    expect(r.from).toBeTruthy();
    expect(r.to).toBeTruthy();
    expect(r.from! <= r.to!).toBe(true);
    expect(defaultRangeBounds(new Date("2026-06-18T12:00:00Z"))).toEqual({
      from: "2026-06-01",
      to: "2026-06-18",
    });
  });
});

describe("currentPeriod", () => {
  it("formats today as YYYY-MM", () => {
    expect(currentPeriod(new Date("2026-06-21T00:00:00Z"))).toBe("2026-06");
  });
});
