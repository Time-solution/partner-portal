/**
 * Date-scope a {@link PortalData} for the cross-tab DATE filter, WITHOUT touching money math.
 *
 * Statement roll-ups read the EXISTING dashboard builders (single source). To honour
 * `useDateRange` we narrow only the financial RECORD arrays by their own date (billing period
 * key, settlement/reversal createdAt, reflection reflectedAt). The ROSTER (`activations`,
 * `partners`, `catalogItems`) is left intact so an active relationship still lists as a line even
 * when no in-range invoice exists — its figures simply read zero. This never recomputes a value;
 * it only includes fewer rows for the existing builders to sum.
 */
import { filterByDate, type DateRange } from "@/lib/filters/dateRange";
import type { PortalData } from "@/lib/data/types";

export function scopeFinancialsByDate(data: PortalData, range: DateRange | undefined): PortalData {
  if (!range || range.mode === "all") return data;
  return {
    ...data,
    billingPeriods: filterByDate(data.billingPeriods, range, (p) => p.periodKey),
    settlementCases: filterByDate(data.settlementCases, range, (c) => c.createdAt),
    reversals: filterByDate(data.reversals, range, (r) => r.createdAt),
    reflectedOrders: filterByDate(data.reflectedOrders, range, (o) => o.reflectedAt),
  };
}
