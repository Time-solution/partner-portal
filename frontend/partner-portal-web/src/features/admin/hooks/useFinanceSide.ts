import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import {
  applyFinanceSideToParams,
  financeSideFromParams,
  type FinanceSide,
} from "@/lib/filters/financeSide";

/**
 * Single source of truth for the active purchase/sales side, backed by the `?side` URL param.
 * Composes with `useDateRange` — each hook only touches its own params, so the date range and the
 * side filter apply together and survive navigation between finance tabs.
 */
export function useFinanceSide(): { side: FinanceSide; setSide: (next: FinanceSide) => void } {
  const [params, setParams] = useSearchParams();
  const side = useMemo(() => financeSideFromParams(params), [params]);
  const setSide = useCallback(
    (next: FinanceSide) => {
      const updated = applyFinanceSideToParams(new URLSearchParams(params), next);
      setParams(updated, { replace: true });
    },
    [params, setParams],
  );
  return { side, setSide };
}
