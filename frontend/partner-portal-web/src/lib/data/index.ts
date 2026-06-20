import { getDataSourceMode } from "./config";
import type { IPortalDataSource } from "./IPortalDataSource";
import { getMockPortalDataSource } from "./mockDataSource";

/** Single seam — screens call this; swap implementation when going live. */
export function getPortalDataSource(): IPortalDataSource {
  const mode = getDataSourceMode();
  if (mode === "live") {
    throw new Error(
      "Live data source is not wired yet. Set VITE_DATA_SOURCE=mock (default).",
    );
  }
  return getMockPortalDataSource();
}

export type { IPortalDataSource };
export * from "./types";
