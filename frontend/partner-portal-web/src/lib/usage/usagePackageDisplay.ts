/**
 * Phase 6b — MERCHANT-facing structured-details projection of a usage package. This is a pure
 * read/display view of what the package already defines (included units, overage/per-use rate,
 * graduated tier thresholds, billing mode, unit, partner-authored explanation). It NEVER
 * recomputes billing and — by going through {@link scopeUsagePackage} with the "merchant" viewer —
 * it carries SELL/fee only: partner buy, margin and per-tier buy rates are structurally absent.
 */
import { scopeUsagePackage, type UsagePackage, type UsagePackageMode } from "./usagePackage";

export interface MerchantPackageTierRow {
  fromQuantity: number;
  /** null/undefined = open-ended top tier. */
  toQuantity?: number | null;
  /** Merchant sell / fee rate per unit. Buy rate is never present. */
  rate?: number;
}

export interface MerchantPackageDisplay {
  id: string;
  name: string;
  mode: UsagePackageMode;
  unitLabel: string;
  includedQuantity: number;
  /** Merchant base price / subscription fee (VAT-inclusive). Absent when zero/unset (pure per-use). */
  baseFee?: number;
  /** Merchant overage / per-use rate per unit. Absent when there is no flat overage. */
  overageRate?: number;
  tiers: MerchantPackageTierRow[];
  /** Partner-authored plain-text note ("خانة للشرح"), if any. */
  explanation?: string;
}

/** Keys that must never appear on a merchant package display (partner buy / margin leakage guard). */
export const MERCHANT_PACKAGE_FORBIDDEN_KEYS = [
  "buy",
  "buyRate",
  "margin",
  "partnerCost",
  "baseBuyAmount",
  "overageBuyAmount",
] as const;

function legFee(leg: { sell?: { amount: number }; fee?: { amount: number } }): number | undefined {
  const amount = leg.sell?.amount ?? leg.fee?.amount;
  return amount && amount > 0 ? amount : undefined;
}

/** Build the merchant structured-details view for a package (SELL/fee only — no recompute). */
export function merchantPackageDisplay(pkg: UsagePackage): MerchantPackageDisplay {
  const scoped = scopeUsagePackage(pkg, "merchant");
  return {
    id: scoped.id,
    name: scoped.name,
    mode: scoped.mode,
    unitLabel: scoped.unitLabel,
    includedQuantity: scoped.includedQuantity,
    baseFee: legFee(scoped.base),
    overageRate: legFee(scoped.overage),
    tiers: scoped.tiers.map((t) => ({
      fromQuantity: t.fromQuantity,
      toQuantity: t.toQuantity,
      rate: t.sell?.amount ?? t.fee?.amount,
    })),
    explanation: pkg.packageExplanation?.trim() || undefined,
  };
}

/** Throw if any forbidden buy/margin key leaked onto a merchant package display (deep). */
export function assertMerchantPackageScope(display: MerchantPackageDisplay): void {
  const visit = (value: unknown): void => {
    if (value == null || typeof value !== "object") return;
    for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
      if ((MERCHANT_PACKAGE_FORBIDDEN_KEYS as readonly string[]).includes(key)) {
        throw new Error(`Forbidden field "${key}" present on merchant package display`);
      }
      visit(child);
    }
  };
  visit(display);
}
