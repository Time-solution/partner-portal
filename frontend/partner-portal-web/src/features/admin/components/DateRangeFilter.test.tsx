import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { DateRangeFilter } from "./DateRangeFilter";
import {
  ALL_DATES,
  applyDateRangeToParams,
  dateRangeFromParams,
  filterByDate,
  type DateRange,
} from "@/lib/filters/dateRange";

describe("DateRangeFilter — shared control", () => {
  it("renders the mode selector with all / month / day / range options (default 'all')", () => {
    const html = renderToStaticMarkup(<DateRangeFilter range={ALL_DATES} onChange={() => {}} lang="ar" />);
    expect(html).toContain('data-testid="date-range-filter"');
    expect(html).toContain('value="all"');
    expect(html).toContain('value="period"');
    expect(html).toContain('value="day"');
    expect(html).toContain('value="range"');
  });

  it("shows a single date input in day mode", () => {
    const range: DateRange = { mode: "day", day: "2026-06-20" };
    const html = renderToStaticMarkup(<DateRangeFilter range={range} onChange={() => {}} lang="en" />);
    expect(html).toContain('type="date"');
    expect(html).toContain('value="2026-06-20"');
  });

  it("shows two date inputs (from/to) in range mode", () => {
    const range: DateRange = { mode: "range", from: "2026-05-01", to: "2026-06-15" };
    const html = renderToStaticMarkup(<DateRangeFilter range={range} onChange={() => {}} lang="en" />);
    expect(html).toContain('value="2026-05-01"');
    expect(html).toContain('value="2026-06-15"');
    expect((html.match(/type="date"/g) ?? []).length).toBe(2);
  });

  it("entering range mode from 'all' persists bounds so from/to inputs render", () => {
    let params = new URLSearchParams("");
    const onChange = (next: DateRange) => {
      params = applyDateRangeToParams(params, next);
    };
    // Simulate selecting "From / To" while on ALL_DATES — the component supplies default bounds.
    onChange({ mode: "range", from: "2026-06-01", to: "2026-06-18" });
    const range = dateRangeFromParams(params);
    expect(range.mode).toBe("range");
    const html = renderToStaticMarkup(<DateRangeFilter range={range} onChange={onChange} lang="en" />);
    expect(html).toContain('value="range"');
    expect((html.match(/type="date"/g) ?? []).length).toBe(2);
  });

  it("shows month input in period mode", () => {
    const range: DateRange = { mode: "period", period: "2026-06" };
    const html = renderToStaticMarkup(<DateRangeFilter range={range} onChange={() => {}} lang="en" />);
    expect(html).toContain('type="month"');
    expect(html).toContain('value="2026-06"');
  });

  it("2026-06-05 → 2026-06-28 range narrows day-precise rows", () => {
    const rows = [
      { id: "a", date: "2026-06-04" },
      { id: "b", date: "2026-06-10" },
      { id: "c", date: "2026-06-28" },
      { id: "d", date: "2026-06-29" },
    ];
    const out = filterByDate(rows, { mode: "range", from: "2026-06-05", to: "2026-06-28" }, (r) => r.date);
    expect(out.map((r) => r.id)).toEqual(["b", "c"]);
  });

  it("is RTL for Arabic, LTR for English", () => {
    const ar = renderToStaticMarkup(<DateRangeFilter range={ALL_DATES} onChange={() => {}} lang="ar" />);
    const en = renderToStaticMarkup(<DateRangeFilter range={ALL_DATES} onChange={() => {}} lang="en" />);
    expect(ar).toContain('dir="rtl"');
    expect(en).toContain('dir="ltr"');
  });
});
