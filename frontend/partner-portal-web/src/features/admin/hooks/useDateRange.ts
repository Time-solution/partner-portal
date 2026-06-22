import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import {
  applyDateRangeToParams,
  dateRangeFromParams,
  type DateRange,
} from "@/lib/filters/dateRange";

/**
 * The SINGLE source of truth for the active date-range filter, backed by the URL search params
 * (`?period` / `?day` / `?from` / `?to`). Every tab reads the same hook, so changing the range on
 * one screen carries to the next via the shared URL state. Mirrors `useScopePartnerIds` — one
 * hook + one pure helper (`filterByDate`) consumed across pages.
 */
export function useDateRange(): { range: DateRange; setRange: (next: DateRange) => void } {
  const [params, setParams] = useSearchParams();
  const range = useMemo(() => dateRangeFromParams(params), [params]);
  const setRange = useCallback(
    (next: DateRange) => {
      const updated = applyDateRangeToParams(new URLSearchParams(params), next);
      setParams(updated, { replace: true });
    },
    [params, setParams],
  );
  return { range, setRange };
}
