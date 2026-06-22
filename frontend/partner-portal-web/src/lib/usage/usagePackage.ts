/**
 * U2 — usage PACKAGE model (frontend mock, mirrors the backend `UsagePackage`). A package defines how a
 * partner's usage-based offering (metered in U1, e.g. "messages") is PRICED. A partner may have multiple
 * optional packages. CONFIG ONLY: holding a package computes NO billing and posts NO journal — the
 * usage→charge calculation is the separately-gated U3 phase.
 *
 * Prices are a BUY/SELL pair (both VAT-inclusive), mirroring Principal catalog items:
 *   • Resale       → buy = what Zahy pays the partner; sell = what Zahy sells. Margin = sell − buy (admin only).
 *   • Subscription → the amount is Zahy's FEE kept entirely (buy = 0); `payer` selects who pays.
 *   • Pure per-use → basePrice 0 + includedQuantity 0 (rate × usage).
 */
import type { Money } from "@/lib/data/types";
import {
  projectCatalogPrice,
  type CatalogPriceViewer,
  type ScopedCatalogPrice,
} from "@/lib/catalog/catalogPriceVisibility";

export type UsagePackageMode = "Resale" | "Subscription";
export type UsagePackageStatus = "Draft" | "Published" | "Archived";
export type UsagePackagePayer = "Merchant" | "Partner";

/**
 * U5 — an OPTIONAL graduated volume tier on the OVERAGE (units beyond includedQuantity), measured in
 * overage units from 0. Carries a buy/sell rate pair like the flat overage (subscription forces buy = 0).
 * No tiers = flat U3 overage (back-compatible). CONFIG ONLY.
 */
export interface UsagePackageTier {
  fromQuantity: number;
  /** null/undefined = open-ended top tier. */
  toQuantity?: number | null;
  buyRate: number;
  sellRate: number;
}

export interface UsagePackage {
  id: string;
  partnerId: string;
  name: string;
  /** Consumed unit — must match the U1 usage unit label, e.g. "messages". */
  unitLabel: string;
  mode: UsagePackageMode;
  currency: string;
  includedQuantity: number;
  /** Recurring base — partner buy side (resale); always 0 for subscription. VAT-inclusive. */
  baseBuyAmount: number;
  /** Recurring base — Zahy sell / the kept fee. VAT-inclusive. */
  baseSellAmount: number;
  /** Per-unit overage beyond included — partner buy side; 0 for subscription. */
  overageBuyAmount: number;
  /** Per-unit overage beyond included — Zahy sell / kept fee. */
  overageSellAmount: number;
  /** Subscription only — who pays the fee. Ignored for resale. */
  payer: UsagePackagePayer;
  status: UsagePackageStatus;
  /** U5 — optional graduated overage tiers (empty/undefined = flat overage). */
  tiers?: UsagePackageTier[];
}

export interface UsagePackageInput {
  partnerId: string;
  name: string;
  unitLabel?: string;
  mode: UsagePackageMode;
  currency?: string;
  includedQuantity: number;
  baseBuyAmount: number;
  baseSellAmount: number;
  overageBuyAmount: number;
  overageSellAmount: number;
  payer?: UsagePackagePayer;
  /** U5 — optional graduated overage tiers. */
  tiers?: UsagePackageTier[];
}

export const DEFAULT_USAGE_PACKAGE_UNIT = "messages";

function money(amount: number, currency: string): Money {
  return { amount: Math.round(amount * 100) / 100, currency, vatInclusive: true };
}

/** One scoped price leg (base or overage). For subscription, only `fee` is present. */
export interface ScopedPackageLeg extends ScopedCatalogPrice {
  /** Subscription fee — visible to every audience; never carries buy/margin. */
  fee?: Money;
}

/** U5 — one audience-scoped overage tier. Rate fields are absent when not visible to the audience. */
export interface ScopedUsageTier extends ScopedPackageLeg {
  fromQuantity: number;
  toQuantity?: number | null;
}

export interface ScopedUsagePackage {
  id: string;
  partnerId: string;
  name: string;
  unitLabel: string;
  mode: UsagePackageMode;
  currency: string;
  includedQuantity: number;
  status: UsagePackageStatus;
  base: ScopedPackageLeg;
  overage: ScopedPackageLeg;
  /** Subscription only — present (with payer) for the fee shape; absent for resale. */
  payer?: UsagePackagePayer;
  /** U5 — optional scoped overage tiers (empty = flat overage). */
  tiers: ScopedUsageTier[];
}

/**
 * Project a package for a viewer — the SINGLE source for scoped pricing visibility, reusing the catalog
 * buy/sell/margin rule. RESALE: partner sees BUY, merchant sees SELL, margin is admin-only and
 * STRUCTURALLY ABSENT for partner/merchant. SUBSCRIPTION: only the fee (+ payer) — no buy/sell/margin.
 */
export function scopeUsagePackage(pkg: UsagePackage, viewer: CatalogPriceViewer): ScopedUsagePackage {
  const common = {
    id: pkg.id,
    partnerId: pkg.partnerId,
    name: pkg.name,
    unitLabel: pkg.unitLabel,
    mode: pkg.mode,
    currency: pkg.currency,
    includedQuantity: pkg.includedQuantity,
    status: pkg.status,
  };

  const scopeTier = (t: UsagePackageTier): ScopedUsageTier =>
    pkg.mode === "Subscription"
      ? { fromQuantity: t.fromQuantity, toQuantity: t.toQuantity, fee: money(t.sellRate, pkg.currency) }
      : {
          fromQuantity: t.fromQuantity,
          toQuantity: t.toQuantity,
          ...projectCatalogPrice(
            { buy: money(t.buyRate, pkg.currency), sell: money(t.sellRate, pkg.currency) },
            viewer,
          ),
        };

  const tiers = (pkg.tiers ?? []).map(scopeTier);

  if (pkg.mode === "Subscription") {
    return {
      ...common,
      base: { fee: money(pkg.baseSellAmount, pkg.currency) },
      overage: { fee: money(pkg.overageSellAmount, pkg.currency) },
      payer: pkg.payer,
      tiers,
    };
  }

  return {
    ...common,
    base: projectCatalogPrice(
      { buy: money(pkg.baseBuyAmount, pkg.currency), sell: money(pkg.baseSellAmount, pkg.currency) },
      viewer,
    ),
    overage: projectCatalogPrice(
      { buy: money(pkg.overageBuyAmount, pkg.currency), sell: money(pkg.overageSellAmount, pkg.currency) },
      viewer,
    ),
    tiers,
  };
}

/** Normalize a package input — subscription has no partner payout, so the buy side is forced to 0. */
export function normalizeUsagePackageInput(input: UsagePackageInput): UsagePackageInput {
  const tiers = normalizeTiers(input.tiers, input.mode);
  if (input.mode === "Subscription") {
    return { ...input, baseBuyAmount: 0, overageBuyAmount: 0, payer: input.payer ?? "Merchant", tiers };
  }
  return { ...input, payer: "Merchant", tiers };
}

/** Sort tiers by lower bound and force the buy side to 0 for subscription (the sell rate is the fee). */
export function normalizeTiers(
  tiers: UsagePackageTier[] | undefined,
  mode: UsagePackageMode,
): UsagePackageTier[] {
  return (tiers ?? [])
    .map((t) => ({
      fromQuantity: t.fromQuantity,
      toQuantity: t.toQuantity ?? null,
      buyRate: mode === "Subscription" ? 0 : t.buyRate,
      sellRate: t.sellRate,
    }))
    .sort((a, b) => a.fromQuantity - b.fromQuantity);
}
