/**
 * SINGLE SOURCE for the cross-tab DATE / DAY-range filter (display/filter only).
 *
 * One filter, consumed by every relevant tab (reports, statements, drill-down, reflected
 * orders, activations, billing). It NEVER touches money math, the settlement engine, or
 * reflection logic — it only NARROWS an already-scoped list of rows by their own date.
 *
 * Modes:
 *  - "all"    → neutral default; includes everything (so an untouched filter = today's behavior)
 *  - "period" → a calendar month "YYYY-MM" (the existing period filter, kept working)
 *  - "day"    → a single calendar day "YYYY-MM-DD"
 *  - "range"  → an inclusive [from..to] of "YYYY-MM-DD" days (either bound optional)
 *
 * The filter LAYERS ON TOP of RBAC/org scope (which is enforced upstream at the data source).
 * `filterByDate` can only return a subset of its input — it can never widen scope.
 */

export type DateRangeMode = "all" | "period" | "range" | "day";

export interface DateRange {
  mode: DateRangeMode;
  /** "YYYY-MM" — used when mode === "period". */
  period?: string;
  /** "YYYY-MM-DD" inclusive lower bound — used when mode === "range". */
  from?: string;
  /** "YYYY-MM-DD" inclusive upper bound — used when mode === "range". */
  to?: string;
  /** "YYYY-MM-DD" — used when mode === "day". */
  day?: string;
}

/** Neutral default: includes every row, so untouched === current behavior (no narrowing). */
export const ALL_DATES: DateRange = { mode: "all" };

/** Today's calendar month "YYYY-MM". */
export function currentPeriod(now: Date = new Date()): string {
  return now.toISOString().slice(0, 7);
}

/** Today's calendar day "YYYY-MM-DD". */
export function currentDay(now: Date = new Date()): string {
  return now.toISOString().slice(0, 10);
}

/** Default [from..to] when entering range mode — first day of current month through today. */
export function defaultRangeBounds(now: Date = new Date()): { from: string; to: string } {
  return { from: `${currentPeriod(now)}-01`, to: currentDay(now) };
}

const MONTH_KEY = /^\d{4}-\d{2}$/;

function monthOf(value: string): string {
  return value.slice(0, 7);
}

function dayOf(value: string): string {
  return value.slice(0, 10);
}

/**
 * Is a row's date inside the selected range?
 * - "all" or an empty/missing selection → always true (no narrowing).
 * - An undated row (empty value) is never hidden by a date filter.
 * - Month-keyed values ("YYYY-MM", e.g. a billing period) match when their MONTH overlaps the
 *   selected day/range — so coarser, month-grained tabs still respond to a day/range pick.
 */
export function inDateRange(value: string | undefined | null, range: DateRange | undefined): boolean {
  if (!range || range.mode === "all") return true;
  const v = (value ?? "").trim();
  if (!v) return true; // undated rows are not narrowed by a date filter

  if (range.mode === "period") {
    return !range.period || monthOf(v) === range.period;
  }

  // Month-keyed row (no day precision): treat the row as the whole month.
  if (MONTH_KEY.test(v)) {
    const month = monthOf(v);
    if (range.mode === "day") return !range.day || monthOf(range.day) === month;
    // range overlap: [month-01 .. month-31] intersects [from .. to]
    const monthStart = `${month}-01`;
    const monthEnd = `${month}-31`;
    if (range.from && monthEnd < range.from) return false;
    if (range.to && monthStart > range.to) return false;
    return true;
  }

  const d = dayOf(v);
  if (range.mode === "day") return !range.day || d === range.day;
  if (range.from && d < range.from) return false;
  if (range.to && d > range.to) return false;
  return true;
}

/**
 * Narrow a list by the selected range using a per-row date accessor. Pure: returns a subset of
 * `items` (or the same array for "all"), preserving order. Never widens — RBAC stays intact.
 */
export function filterByDate<T>(
  items: T[],
  range: DateRange | undefined,
  getDate: (item: T) => string | undefined | null,
): T[] {
  if (!range || range.mode === "all") return items;
  return items.filter((item) => inDateRange(getDate(item), range));
}

/**
 * Representative calendar month for a range — used by month-grained read models (Reports,
 * drill-down) whose engine math accepts a single "YYYY-MM". Returns undefined for "all" so the
 * caller can fall back to its own default (e.g. the latest period) and preserve current behavior.
 */
export function rangeMonth(range: DateRange | undefined): string | undefined {
  if (!range) return undefined;
  if (range.mode === "period") return range.period || undefined;
  if (range.mode === "day") return range.day ? monthOf(range.day) : undefined;
  if (range.mode === "range") {
    const v = range.to || range.from;
    return v ? monthOf(v) : undefined;
  }
  return undefined; // "all"
}

/**
 * Compact, non-i18n label for the active range — a month ("YYYY-MM"), a day ("YYYY-MM-DD"), or a
 * "from → to" window. Used as the display tag next to a translated prefix (period/range context).
 * "all" falls back to the supplied period (the legacy latest-period default) so labels never blank.
 */
export function describeRange(range: DateRange | undefined, fallbackPeriod = ""): string {
  if (!range || range.mode === "all") return fallbackPeriod;
  if (range.mode === "period") return range.period || fallbackPeriod;
  if (range.mode === "day") return range.day || fallbackPeriod;
  const from = range.from || "…";
  const to = range.to || "…";
  return `${from} → ${to}`;
}

const PARAM_KEYS = ["from", "to", "day", "period"] as const;

/**
 * Read a DateRange from URL search params (the shared cross-tab state). Compatible with the
 * existing Reports `?period=` param. Presence of `day` → day mode; `from`/`to` → range;
 * `period` → period; none → all.
 */
export function dateRangeFromParams(params: URLSearchParams): DateRange {
  const day = params.get("day") || undefined;
  const from = params.get("from") || undefined;
  const to = params.get("to") || undefined;
  const period = params.get("period") || undefined;
  if (day) return { mode: "day", day };
  if (from || to) return { mode: "range", from, to };
  if (period) return { mode: "period", period };
  return { mode: "all" };
}

/** Write a DateRange onto a URLSearchParams (clears the other date keys first). Mutates + returns. */
export function applyDateRangeToParams(params: URLSearchParams, range: DateRange): URLSearchParams {
  for (const key of PARAM_KEYS) params.delete(key);
  if (range.mode === "period" && range.period) params.set("period", range.period);
  else if (range.mode === "day" && range.day) params.set("day", range.day);
  else if (range.mode === "range") {
    const defaults = defaultRangeBounds();
    params.set("from", range.from ?? defaults.from);
    params.set("to", range.to ?? defaults.to);
  }
  return params;
}
