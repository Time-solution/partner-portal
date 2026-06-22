/**
 * SINGLE SOURCE for the PURCHASE vs SALES finance filter (display/filter only).
 *
 * Classifies the existing report accounts into a sell-side / buy-side and gates what each role
 * may even see. Composes WITH the date-range filter — both narrow the same read-model data, and
 * neither recomputes money or touches the settlement engine / reflection.
 *
 * Account map (uses the existing `ReportAccount` names — nothing invented):
 *   SALES  (sell / revenue / receivable): RevenueNetSell 4100, FeeRevenue 4200,
 *                                          MerchantReceivable 1200, VatOutput 2200
 *   PURCHASE (buy / cost / payable):       PartnerCost (COGS) 5100, PartnerPayable (AP) 2100,
 *                                          VatInput 1300
 *   NEUTRAL (shared context, "Both" only): Cash, CollectionsReceivable, MerchantPayable
 *
 * SCOPE (never widens): the filter layers ON TOP of org/data scope. A partner's side is PURCHASE
 * and a merchant's side is SALES; the opposite side and the resale MARGIN are forbidden and stay
 * absent regardless of the toggle. Accountant / PlatformAdmin see both sides + margin.
 */
import { ReportAccount } from "@/lib/reports/settlementReports";
import type { PortalRole } from "@/lib/rbac/portalRoles";

export type FinanceSide = "both" | "sales" | "purchase";
export type AccountSide = "sales" | "purchase" | "neutral";

export const FINANCE_SIDES: FinanceSide[] = ["both", "sales", "purchase"];

/** Sell-side accounts: revenue + receivable + output VAT. */
export const SALES_ACCOUNTS: ReadonlySet<string> = new Set([
  ReportAccount.RevenueNetSell,
  ReportAccount.FeeRevenue,
  ReportAccount.MerchantReceivable,
  ReportAccount.VatOutput,
]);

/** Buy-side accounts: partner cost (COGS) + partner payable (AP) + input VAT. */
export const PURCHASE_ACCOUNTS: ReadonlySet<string> = new Set([
  ReportAccount.PartnerCost,
  ReportAccount.PartnerPayable,
  ReportAccount.VatInput,
]);

export function accountSide(account: string): AccountSide {
  if (SALES_ACCOUNTS.has(account)) return "sales";
  if (PURCHASE_ACCOUNTS.has(account)) return "purchase";
  return "neutral";
}

/** What the current viewer's ROLE is permitted to see — the hard ceiling the toggle cannot exceed. */
export interface FinanceSideScope {
  canSeeSales: boolean;
  canSeePurchase: boolean;
  /** Resale margin (sell − cost) is a platform figure only. */
  canSeeMargin: boolean;
}

const PLATFORM_SCOPE: FinanceSideScope = { canSeeSales: true, canSeePurchase: true, canSeeMargin: true };
const PARTNER_SCOPE: FinanceSideScope = { canSeeSales: false, canSeePurchase: true, canSeeMargin: false };
const MERCHANT_SCOPE: FinanceSideScope = { canSeeSales: true, canSeePurchase: false, canSeeMargin: false };

/**
 * Role → side scope. Partner roles are PURCHASE-only (no sell/margin); the merchant preview is
 * SALES-only (no buy/margin); accountant/admin see both + margin. Unknown roles fall back to the
 * platform default (matches the live-auth PlatformAdmin fallback) — callers only ever pass roles
 * that already cleared the finance permission gate.
 */
export function financeSideScope(role: PortalRole | string): FinanceSideScope {
  switch (role) {
    case "PartnerSuccessManager":
    case "PartnerFinance":
      return PARTNER_SCOPE;
    case "MerchantPreview":
      return MERCHANT_SCOPE;
    case "PlatformAdmin":
    case "Accountant":
    default:
      return PLATFORM_SCOPE;
  }
}

const sideShowsSales = (side: FinanceSide) => side === "both" || side === "sales";
const sideShowsPurchase = (side: FinanceSide) => side === "both" || side === "purchase";

/** Sell-side figures (fee revenue, merchant receivable, merchant statements) visible? */
export function salesVisible(side: FinanceSide, scope: FinanceSideScope): boolean {
  return scope.canSeeSales && sideShowsSales(side);
}

/** Buy-side figures (partner cost/payable, partner statements) visible? */
export function purchaseVisible(side: FinanceSide, scope: FinanceSideScope): boolean {
  return scope.canSeePurchase && sideShowsPurchase(side);
}

/** Resale margin visible? Platform-only, and only in the combined "Both" view. */
export function marginVisible(side: FinanceSide, scope: FinanceSideScope): boolean {
  return scope.canSeeMargin && side === "both";
}

/** Net VAT to ZATCA is a combined figure — shown only in the "Both" view. */
export function netVatVisible(side: FinanceSide): boolean {
  return side === "both";
}

/**
 * Platform Contribution / P&L is a MANAGEMENT both-sides statement (sales − cost = gross). Only a
 * role that can see BOTH sides AND the margin (PlatformAdmin / Accountant) may view it — partners
 * and merchants never can (their scope blocks margin). Role gate only; never widens scope.
 */
export function canSeeContribution(scope: FinanceSideScope): boolean {
  return scope.canSeeSales && scope.canSeePurchase && scope.canSeeMargin;
}

/**
 * Is a single account row visible for the selected side AND the viewer's role scope?
 * Forbidden sides are absent regardless of the toggle; neutral rows show only in "Both".
 */
export function accountVisible(account: string, side: FinanceSide, scope: FinanceSideScope): boolean {
  const s = accountSide(account);
  if (s === "sales") return salesVisible(side, scope);
  if (s === "purchase") return purchaseVisible(side, scope);
  return side === "both"; // neutral (VAT-neutral clearing / cash) is shared context only
}

/** Filter a list of account-bearing rows (trial balance / order group / drill-down) by side + scope. */
export function filterAccountsBySide<T extends { account: string }>(
  rows: T[],
  side: FinanceSide,
  scope: FinanceSideScope,
): T[] {
  return rows.filter((row) => accountVisible(row.account, side, scope));
}

export const FINANCE_SIDE_PARAM = "side";

export function financeSideFromParams(params: URLSearchParams): FinanceSide {
  const raw = params.get(FINANCE_SIDE_PARAM);
  return raw === "sales" || raw === "purchase" ? raw : "both";
}

/** Write the side onto a URLSearchParams (mutates + returns). "both" is the default → no param. */
export function applyFinanceSideToParams(params: URLSearchParams, side: FinanceSide): URLSearchParams {
  if (side === "both") params.delete(FINANCE_SIDE_PARAM);
  else params.set(FINANCE_SIDE_PARAM, side);
  return params;
}
