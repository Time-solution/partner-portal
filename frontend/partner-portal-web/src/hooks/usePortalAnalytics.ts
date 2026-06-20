import { useCallback, useEffect, useState } from "react";
import { loadPortalChartAnalytics, type PortalChartAnalytics } from "@/lib/analytics/chartAnalytics";
import { subscribePortalDataChanged } from "@/lib/data/portalDataEvents";

export function usePortalAnalytics() {
  const [analytics, setAnalytics] = useState<PortalChartAnalytics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      const next = await loadPortalChartAnalytics();
      setAnalytics(next);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load analytics");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
    return subscribePortalDataChanged(() => {
      void refresh();
    });
  }, [refresh]);

  return { analytics, loading, error, refresh };
}
