/**
 * SINGLE SOURCE for the date-grained scoping that feeds Reports + Finance drill-down.
 *
 * Two grains, each matched to the data — and the settlement engine math is NEVER touched
 * (`settlementReports.ts` is unchanged). We only narrow the SOURCE `PortalData` arrays by their
 * own date BEFORE `toReportEntries`, then derive the SAME journals from fewer records:
 *
 *  1. SALES / PURCHASE / margin / settlement → TRUE day-to-day. We day-filter the source via
 *     `scopeFinancialsByDate` (settlement cases / reversals on `createdAt`, day-precise; reflected
 *     orders on `reflectedAt`; subscription periods on `periodKey`, which is month-only so they
 *     month-overlap). A range spanning months AGGREGATES every in-range case — no single-month
 *     collapse (the old `rangeMonth` bug that dropped a month is gone for these figures).
 *
 *  2. VAT (net VAT to ZATCA = 2200 − 1300) → STAYS month/period-based. Output VAT mixes
 *     day-precise settlement VAT with month-only subscription VAT, and net VAT is a filing-period
 *     concept — day-slicing it would be a mixed-resolution number that is not a real regulatory
 *     figure. So the VAT entries are the FULL entries whose MONTH overlaps the range (never
 *     day-sliced); a day pick still shows the whole touched period's VAT, labeled as a period.
 */
import type { PortalData } from "@/lib/data/types";
import { type DateRange, inDateRange } from "@/lib/filters/dateRange";
import { scopeFinancialsByDate } from "@/lib/statements/dateScopedData";
import { toReportEntries, type ReportEntry } from "@/lib/reports/settlementReports";

export interface ScopedReportEntries {
  /** The range actually applied. Untouched "all" collapses to the latest period (back-compat). */
  effectiveRange: DateRange;
  /** Day-to-day scoped entries — SALES / PURCHASE / margin / settlement. Engine math unchanged. */
  entries: ReportEntry[];
  /** Period/month-overlap entries — VAT only. Never day-sliced (filing-period figure). */
  vatEntries: ReportEntry[];
  /** True when a sub-month grain (day/range) is active — subscription fees can only month-overlap. */
  dayGrain: boolean;
}

/**
 * Produce the day-scoped entries (sales/purchase) and the period-scoped entries (VAT) for a range.
 * Entity scope (partner/merchant) is layered by the caller ON TOP of these — it never widens.
 */
export function scopeReportEntries(
  data: PortalData,
  range: DateRange,
  fallbackPeriod = "",
): ScopedReportEntries {
  // Untouched "all" keeps the legacy latest-period default → no behaviour change when not filtering.
  const effectiveRange: DateRange =
    range.mode === "all" && fallbackPeriod ? { mode: "period", period: fallbackPeriod } : range;

  // Day-to-day: narrow the SOURCE arrays, THEN derive entries (same math, fewer records).
  const entries = toReportEntries(scopeFinancialsByDate(data, effectiveRange));

  // VAT: month-overlap on the FULL entries (e.period is always a "YYYY-MM" key → never day-sliced).
  const full = toReportEntries(data);
  const vatEntries = full.filter((e) => inDateRange(e.period, effectiveRange));

  const dayGrain = effectiveRange.mode === "day" || effectiveRange.mode === "range";

  return { effectiveRange, entries, vatEntries, dayGrain };
}
