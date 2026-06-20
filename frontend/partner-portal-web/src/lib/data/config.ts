/** Data source mode — default MOCK. Switch to `live` in a later phase without touching screens. */
export type DataSourceMode = "mock" | "live";

export function getDataSourceMode(): DataSourceMode {
  const raw = import.meta.env.VITE_DATA_SOURCE?.toLowerCase();
  return raw === "live" ? "live" : "mock";
}

export const isMockDataSource = (): boolean => getDataSourceMode() === "mock";
