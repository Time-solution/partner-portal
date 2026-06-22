/**
 * U1 — usage-metering model (frontend mock, mirrors the backend `UsageRecord`). Records how many UNITS
 * a merchant consumed for a partner in a period (e.g. message count) as a QUANTITY. PURE DATA: no fee is
 * computed and no journal is posted here — usage-based pricing/posting is the separately-gated U3 phase.
 */

/** A single (incremental) usage record. Keyed to partner + merchant + period; rows accumulate. */
export interface UsageRecord {
  id: string;
  partnerId: string;
  /** The merchant (tenant) that consumed the units. */
  tenantId: string;
  /** Period as YYYY-MM (mirrors the backend SettlementPeriod.ToString()). */
  period: string;
  /** The consumed unit, e.g. "messages". Free-form label — no pricing attached. */
  unitLabel: string;
  /** Quantity consumed in this record (non-negative). */
  quantity: number;
  /** Provenance: "seed" / "manual" / "partner-feed" (the gated auto-feed seam). */
  source: string;
}

/** Form/append input — the next code/id is assigned by the store. */
export interface UsageRecordInput {
  partnerId: string;
  tenantId: string;
  period: string;
  unitLabel?: string;
  quantity: number;
  source?: string;
}

export const DEFAULT_UNIT_LABEL = "units";
export const DEFAULT_USAGE_SOURCE = "manual";

/**
 * AUTO-FEED — SEAM ONLY (not wired). A real partner/gateway usage feed WOULD push usage records through
 * here. Mirrors the Track B gateway-route seam: the flag stays false and the push path throws, proving
 * no live feed silently ingests usage. Do NOT implement a real feed here (that is the gated phase).
 */
export const USAGE_FEED_WIRED = false;

/** NOT IMPLEMENTED — throws to keep the seam honest (the gated phase wires a real feed). */
export function usageFeedAutoPush(_signals: UsageRecordInput[]): never {
  throw new Error(
    "Usage auto-feed is a SEAM ONLY and is not wired. A real partner/gateway usage feed is enabled " +
      "in the separately-gated phase. Use seeded or manual usage records.",
  );
}
