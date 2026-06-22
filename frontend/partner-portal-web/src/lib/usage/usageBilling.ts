/**
 * U3 — USAGE BILLING CALCULATION (frontend mirror of the backend `UsageBillingCalculator`).
 * COMPUTE-ONLY: computes the period amount from a U2 package + U1 usage, then routes it to the EXISTING
 * posting template per the package mode (Principal for resale, Fee for subscription). NOTHING is posted —
 * this produces the journal/amount for preview only (PostingEnabled stays OFF, no money moves).
 *
 *   amount = proratedBase + max(0, usage − includedQuantity) × overageRate   (VAT-inclusive)
 *
 *   • RESALE       → buy + sell legs → Principal (sell 1200/4100/2200, buy 5100/1300/2100); margin = sell − buy.
 *   • SUBSCRIPTION → fee leg (payer-selectable) → Fee (1200/1250 → 4200 + 2200).
 *   • Pure per-use → base 0 + included 0 (rate × usage).
 *
 * VAT is the single `splitInclusiveVat` helper (imported, NOT re-implemented). The BASE is pro-rated by
 * ACTUAL CALENDAR DAYS; usage (overage) is whole units already counted to the deactivation moment.
 * Money: 4dp intermediate, 2dp final, away-from-zero.
 */
import { VAT_RATE, splitInclusiveVat } from "@/lib/reports/settlementReports";
import {
  projectCatalogPrice,
  type CatalogPriceViewer,
  type ScopedCatalogPrice,
} from "@/lib/catalog/catalogPriceVisibility";
import type { Money } from "@/lib/data/types";
import type { UsagePackage, UsagePackagePayer, UsagePackageTier } from "./usagePackage";

const round2 = (n: number) => {
  const sign = n < 0 ? -1 : 1;
  return (sign * Math.round(Math.abs(n) * 100)) / 100;
};
const round4 = (n: number) => {
  const sign = n < 0 ? -1 : 1;
  return (sign * Math.round(Math.abs(n) * 10000)) / 10000;
};

/** U5 — one graduated tier's contribution to the overage (display detail). */
export interface UsageBillingTierLine {
  fromQuantity: number;
  toQuantity?: number | null;
  rate: number;
  units: number;
  amountInclusive: number;
}

/** One computed price leg (base + overage), all VAT-inclusive. */
export interface UsageBillingLeg {
  /** Base after calendar-day proration. */
  baseInclusive: number;
  /** max(0, usage − included) — whole units counted to deactivation. */
  overageExcess: number;
  overageRate: number;
  overageInclusive: number;
  /** base + overage (2dp). */
  totalInclusive: number;
  /** U5 — per-tier detail when the package is tiered; empty for flat overage. */
  tiers: UsageBillingTierLine[];
}

/** A computed journal line (mirrors a backend PostingLine) — COMPUTE ONLY, never posted. */
export interface UsageBillingLine {
  account: string;
  direction: "Debit" | "Credit";
  amount: number;
}

export type UsageBillingTemplate = "Principal" | "Fee";

export interface UsageBillingResult {
  packageId: string;
  mode: UsagePackage["mode"];
  currency: string;
  usage: number;
  activeDays: number;
  daysInMonth: number;
  /** Resale only — partner payout leg. */
  buy?: UsageBillingLeg;
  /** Resale only — merchant charge leg. */
  sell?: UsageBillingLeg;
  /** Subscription only — the kept fee leg. */
  fee?: UsageBillingLeg;
  /** Subscription only — who pays. */
  payer?: UsagePackagePayer;
  /** Inclusive margin = sell − buy (resale only; 0 for subscription). */
  marginInclusive: number;
  /** Which EXISTING template this routes to. */
  template: UsageBillingTemplate;
  /** The computed (un-posted) journal. */
  lines: UsageBillingLine[];
}

/** Calendar days in a "YYYY-MM" period's month. */
export function daysInPeriod(period: string): number {
  const [year, month] = period.split("-").map((p) => Number(p));
  return new Date(year, month, 0).getDate();
}

/**
 * GRADUATED (marginal) overage — each tier's units are charged at THAT tier's rate. Units-in-tier =
 * max(0, min(excess, to) − from); open-ended top tier (to null) takes the remainder. 4dp per tier, 2dp final.
 */
function graduatedOverage(
  excess: number,
  tiers: UsagePackageTier[],
  rateOf: (t: UsagePackageTier) => number,
): { overageInclusive: number; lines: UsageBillingTierLine[] } {
  const lines: UsageBillingTierLine[] = [];
  let sum4 = 0;
  for (const tier of [...tiers].sort((a, b) => a.fromQuantity - b.fromQuantity)) {
    const upper = tier.toQuantity ?? excess;
    const units = Math.max(0, Math.min(excess, upper) - tier.fromQuantity);
    if (units <= 0) continue;
    const rate = rateOf(tier);
    const amount4 = round4(units * rate);
    sum4 += amount4;
    lines.push({ fromQuantity: tier.fromQuantity, toQuantity: tier.toQuantity, rate, units, amountInclusive: round2(amount4) });
  }
  return { overageInclusive: round2(sum4), lines };
}

function computeLeg(
  baseAmount: number,
  includedQuantity: number,
  overageRate: number,
  usage: number,
  activeDays: number,
  daysInMonth: number,
  tiers: UsagePackageTier[],
  rateOf: (t: UsagePackageTier) => number,
): UsageBillingLeg {
  const baseInclusive =
    activeDays >= daysInMonth ? round2(baseAmount) : round2((baseAmount * activeDays) / daysInMonth);
  const overageExcess = Math.max(0, usage - includedQuantity);

  let overageInclusive: number;
  let tierLines: UsageBillingTierLine[];
  if (tiers.length === 0) {
    // Back-compat: flat overage, identical to U3.
    overageInclusive = round2(round4(overageExcess * overageRate)); // 4dp intermediate → 2dp final
    tierLines = [];
  } else {
    ({ overageInclusive, lines: tierLines } = graduatedOverage(overageExcess, tiers, rateOf));
  }

  const totalInclusive = round2(baseInclusive + overageInclusive);
  return { baseInclusive, overageExcess, overageRate, overageInclusive, totalInclusive, tiers: tierLines };
}

/** Principal journal mirror (sell 1200/4100/2200, buy 5100/1300/2100). */
function principalLines(sellGross: number, buyGross: number, vatRate: number): UsageBillingLine[] {
  const { exVat: sellNet, vat: outputVat } = splitInclusiveVat(sellGross, vatRate);
  const { exVat: buyNet, vat: inputVat } = splitInclusiveVat(buyGross, vatRate);
  return [
    { account: "1200", direction: "Debit", amount: round2(sellGross) },
    { account: "4100", direction: "Credit", amount: sellNet },
    { account: "2200", direction: "Credit", amount: outputVat },
    { account: "5100", direction: "Debit", amount: buyNet },
    { account: "1300", direction: "Debit", amount: inputVat },
    { account: "2100", direction: "Credit", amount: round2(buyGross) },
  ];
}

/** Fee journal mirror (Dr 1200/1250 → Cr 4200 + 2200). */
function feeLines(feeGross: number, payer: UsagePackagePayer, vatRate: number): UsageBillingLine[] {
  const { exVat: feeNet, vat: outputVat } = splitInclusiveVat(feeGross, vatRate);
  const receivable = payer === "Partner" ? "1250" : "1200";
  return [
    { account: receivable, direction: "Debit", amount: round2(feeGross) },
    { account: "4200", direction: "Credit", amount: feeNet },
    { account: "2200", direction: "Credit", amount: outputVat },
  ];
}

/**
 * Compute the period billing for one merchant × package × period. `usage` is the U1 usageForPeriod total;
 * `activeDays` null/undefined means the full month. COMPUTE-ONLY — the returned `lines` are never posted.
 */
export function computeUsageBilling(
  pkg: UsagePackage,
  usage: number,
  period: string,
  activeDays?: number,
  vatRate: number = VAT_RATE,
): UsageBillingResult {
  if (usage < 0) {
    throw new Error("Usage must not be negative.");
  }
  const daysInMonth = daysInPeriod(period);
  const days = activeDays ?? daysInMonth;
  if (days < 0 || days > daysInMonth) {
    throw new Error(`Active days ${days} out of range for ${daysInMonth}-day month.`);
  }

  const common = {
    packageId: pkg.id,
    mode: pkg.mode,
    currency: pkg.currency,
    usage,
    activeDays: days,
    daysInMonth,
  };

  const tiers = pkg.tiers ?? [];

  if (pkg.mode === "Subscription") {
    const fee = computeLeg(pkg.baseSellAmount, pkg.includedQuantity, pkg.overageSellAmount, usage, days, daysInMonth, tiers, (t) => t.sellRate);
    return {
      ...common,
      fee,
      payer: pkg.payer,
      marginInclusive: 0,
      template: "Fee",
      lines: feeLines(fee.totalInclusive, pkg.payer, vatRate),
    };
  }

  const buy = computeLeg(pkg.baseBuyAmount, pkg.includedQuantity, pkg.overageBuyAmount, usage, days, daysInMonth, tiers, (t) => t.buyRate);
  const sell = computeLeg(pkg.baseSellAmount, pkg.includedQuantity, pkg.overageSellAmount, usage, days, daysInMonth, tiers, (t) => t.sellRate);
  return {
    ...common,
    buy,
    sell,
    marginInclusive: round2(sell.totalInclusive - buy.totalInclusive),
    template: "Principal",
    lines: principalLines(sell.totalInclusive, buy.totalInclusive, vatRate),
  };
}

/** Trial-balance net of the computed journal (debits − credits). Always 0 for a balanced compute. */
export function billingTrialBalanceNet(result: UsageBillingResult): number {
  const debits = result.lines.filter((l) => l.direction === "Debit").reduce((s, l) => s + l.amount, 0);
  const credits = result.lines.filter((l) => l.direction === "Credit").reduce((s, l) => s + l.amount, 0);
  return round2(debits - credits);
}

/** Audience-scoped billing view — reuses the U2 buy/sell/margin rule. */
export interface ScopedUsageBilling extends ScopedCatalogPrice {
  /** Subscription fee — visible to every audience; never carries buy/margin. */
  fee?: Money;
  payer?: UsagePackagePayer;
}

/**
 * Project the computed totals for a viewer (SINGLE source, reusing `projectCatalogPrice`):
 *   • RESALE       → partner sees BUY, merchant sees SELL, margin is admin/accountant only (absent otherwise).
 *   • SUBSCRIPTION → only the fee (+ payer) for everyone — no buy/sell/margin.
 */
export function scopeUsageBilling(result: UsageBillingResult, viewer: CatalogPriceViewer): ScopedUsageBilling {
  if (result.mode === "Subscription") {
    return {
      fee: { amount: result.fee?.totalInclusive ?? 0, currency: result.currency, vatInclusive: true },
      payer: result.payer,
    };
  }
  return projectCatalogPrice(
    {
      buy: { amount: result.buy?.totalInclusive ?? 0, currency: result.currency, vatInclusive: true },
      sell: { amount: result.sell?.totalInclusive ?? 0, currency: result.currency, vatInclusive: true },
    },
    viewer,
  );
}
