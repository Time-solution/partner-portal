/**
 * U4 — INVOICE USAGE BREAKDOWN. Turns a U2 package + U1 usage + the U4 selection's active window into the
 * line a merchant/partner sees on their invoice/statement:
 *
 *   base (prorated if partial period) + max(0, usage − included) × overage rate = total  (VAT split shown)
 *
 * It DOES NOT recompute money — it reads U3's {@link computeUsageBilling} leg verbatim and only picks the
 * AUDIENCE-scoped leg (merchant→sell, partner→buy for resale, fee for subscription; margin admin-only) and
 * splits VAT via the SINGLE `splitInclusiveVat` helper. Display only — nothing is posted.
 */
import { VAT_RATE, splitInclusiveVat } from "@/lib/reports/settlementReports";
import type { CatalogPriceViewer } from "@/lib/catalog/catalogPriceVisibility";
import {
  computeUsageBilling,
  daysInPeriod,
  type UsageBillingLeg,
  type UsageBillingTierLine,
} from "./usageBilling";
import type { UsagePackage, UsagePackagePayer } from "./usagePackage";

export type UsageInvoiceLegKind = "sell" | "buy" | "fee";

export interface UsageInvoiceBreakdown {
  packageId: string;
  packageName: string;
  unitLabel: string;
  mode: UsagePackage["mode"];
  currency: string;
  period: string;
  usage: number;
  includedQuantity: number;
  activeDays: number;
  daysInMonth: number;
  /** True when the base was day-prorated (active only part of the period). */
  prorated: boolean;
  /** Subscription only — who pays the fee. */
  payer?: UsagePackagePayer;
  /** Which side the viewer is billed/sees: sell (merchant/admin), buy (partner resale), fee (subscription). */
  legKind: UsageInvoiceLegKind;
  baseInclusive: number;
  overageExcess: number;
  overageRate: number;
  overageInclusive: number;
  /** U5 — per-tier overage detail (scoped to the viewer's leg); empty for flat packages. */
  tiers: UsageBillingTierLine[];
  totalInclusive: number;
  exVat: number;
  vat: number;
  /** Admin/accountant only — sell − buy (resale). Absent for partner/merchant (structural). */
  marginInclusive?: number;
}

/**
 * Calendar days a package was active inside a "YYYY-MM" period, from the selection window
 * (activatedAt..endedAt). Full month when active before the month with no end. Used for base proration.
 */
export function activeDaysInPeriod(period: string, activatedAt?: string, endedAt?: string): number {
  const dim = daysInPeriod(period);
  const monthStart = `${period}-01`;
  const monthEnd = `${period}-${String(dim).padStart(2, "0")}`;
  const startDay = (activatedAt ?? monthStart).slice(0, 10);
  const endDay = (endedAt ?? monthEnd).slice(0, 10);
  const effStart = startDay < monthStart ? monthStart : startDay;
  const effEnd = endDay > monthEnd ? monthEnd : endDay;
  if (effStart > monthEnd || effEnd < monthStart) return 0;
  const days = Math.round((Date.parse(effEnd) - Date.parse(effStart)) / 86_400_000) + 1;
  return Math.max(0, Math.min(dim, days));
}

const canSeeMargin = (viewer: CatalogPriceViewer) => viewer === "admin" || viewer === "accountant";

/**
 * Build the audience-scoped invoice breakdown. `usage` is the U1 usageForPeriod total; the window
 * (activatedAt/endedAt) drives base proration. Numbers come straight from U3 — no recompute.
 */
export function buildUsageInvoiceBreakdown(
  pkg: UsagePackage,
  usage: number,
  period: string,
  options: { viewer: CatalogPriceViewer; activatedAt?: string; endedAt?: string },
): UsageInvoiceBreakdown {
  const { viewer } = options;
  const activeDays = activeDaysInPeriod(period, options.activatedAt, options.endedAt);
  const result = computeUsageBilling(pkg, usage, period, activeDays, VAT_RATE);

  let leg: UsageBillingLeg | undefined;
  let legKind: UsageInvoiceLegKind;
  if (result.mode === "Subscription") {
    leg = result.fee;
    legKind = "fee";
  } else if (viewer === "partner") {
    leg = result.buy; // partner sees the BUY side (what Zahy pays them); sell/margin absent
    legKind = "buy";
  } else {
    leg = result.sell; // merchant + admin/accountant see the SELL side
    legKind = "sell";
  }

  const total = leg?.totalInclusive ?? 0;
  const { exVat, vat } = splitInclusiveVat(total, VAT_RATE);

  return {
    packageId: pkg.id,
    packageName: pkg.name,
    unitLabel: pkg.unitLabel,
    mode: pkg.mode,
    currency: pkg.currency,
    period,
    usage,
    includedQuantity: pkg.includedQuantity,
    activeDays: result.activeDays,
    daysInMonth: result.daysInMonth,
    prorated: result.activeDays < result.daysInMonth,
    payer: result.payer,
    legKind,
    baseInclusive: leg?.baseInclusive ?? 0,
    overageExcess: leg?.overageExcess ?? 0,
    overageRate: leg?.overageRate ?? 0,
    overageInclusive: leg?.overageInclusive ?? 0,
    tiers: leg?.tiers ?? [],
    totalInclusive: total,
    exVat,
    vat,
    // Margin is admin/accountant-only for resale — structurally absent otherwise.
    marginInclusive:
      result.mode === "Resale" && canSeeMargin(viewer) ? result.marginInclusive : undefined,
  };
}
