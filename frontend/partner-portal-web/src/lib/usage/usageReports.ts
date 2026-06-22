/**
 * U1 read models over usage records (frontend mirror of the backend `UsageReports`) — READ ONLY, pure,
 * no money. Records ACCUMULATE: several incremental rows for the same partner+merchant+period sum.
 * Usage is also row-scoped (respecting the existing RBAC shape): accountant/admin see all; a partner sees
 * only their own merchants' usage; a merchant sees only their own.
 */
import type { UsageRecord } from "./usageRecord";
import { DEFAULT_UNIT_LABEL } from "./usageRecord";

const r2 = (n: number) => Math.round(n * 100) / 100;

/** Who is asking — drives row-level scope over usage records. */
export type UsageViewer =
  | { kind: "platform" }
  | { kind: "partner"; partnerId: string }
  | { kind: "merchant"; tenantId: string };

export interface UsageMerchantTotal {
  tenantId: string;
  unitLabel: string;
  quantity: number;
}

export interface UsagePartnerRollup {
  partnerId: string;
  period: string;
  perMerchant: UsageMerchantTotal[];
  totalQuantity: number;
}

/** Apply RBAC row-scope: platform sees all; partner sees own merchants; merchant sees own. */
export function scopeUsage(records: UsageRecord[], viewer: UsageViewer): UsageRecord[] {
  switch (viewer.kind) {
    case "platform":
      return records.slice();
    case "partner":
      return records.filter((r) => r.partnerId === viewer.partnerId);
    case "merchant":
      return records.filter((r) => r.tenantId === viewer.tenantId);
    default:
      return [];
  }
}

/**
 * Total units a merchant consumed for a partner in a period — sums ALL matching (incremental) records.
 * Returns 0 when none match.
 */
export function usageForPeriod(
  records: UsageRecord[],
  partnerId: string,
  tenantId: string,
  period: string,
): number {
  return r2(
    records
      .filter((r) => r.partnerId === partnerId && r.tenantId === tenantId && r.period === period)
      .reduce((sum, r) => sum + r.quantity, 0),
  );
}

/** Per-partner / per-period rollup: each merchant's summed usage + the partner grand total. */
export function partnerPeriodRollup(
  records: UsageRecord[],
  partnerId: string,
  period: string,
): UsagePartnerRollup {
  const inScope = records.filter((r) => r.partnerId === partnerId && r.period === period);

  const byMerchant = new Map<string, UsageMerchantTotal>();
  for (const r of inScope) {
    const line = byMerchant.get(r.tenantId) ?? {
      tenantId: r.tenantId,
      unitLabel: r.unitLabel || DEFAULT_UNIT_LABEL,
      quantity: 0,
    };
    line.quantity = r2(line.quantity + r.quantity);
    byMerchant.set(r.tenantId, line);
  }

  const perMerchant = Array.from(byMerchant.values()).sort((a, b) => a.tenantId.localeCompare(b.tenantId));
  const totalQuantity = r2(perMerchant.reduce((s, m) => s + m.quantity, 0));
  return { partnerId, period, perMerchant, totalQuantity };
}
