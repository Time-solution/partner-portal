/**
 * MERCHANT STATEMENT — a SALES-side roll-up across ALL the merchant's partners.
 *
 * Invoices are SEPARATE PER PARTNER (existing decision), so this is a roll-up VIEW (NOT a merged
 * single invoice): one line per active partner relationship (partner, service/tier, billed, paid,
 * remaining) plus a combined summary. It assembles the EXISTING merchant dashboard read model
 * ({@link buildMerchantActivePartners}) on date-scoped data — no second statement math, no recompute.
 *
 * Scope: merchant = SALES/receivable only. The line shape already excludes buy/cost/payable/margin
 * (see MERCHANT_ACTIVE_PARTNER_FORBIDDEN_KEYS + assertMerchantRowFieldScope) — those fields stay
 * ABSENT here too, satisfying the merchant finance-side scope without a toggle.
 */
import { type DateRange } from "@/lib/filters/dateRange";
import type { PortalData } from "@/lib/data/types";
import {
  buildMerchantActivePartners,
  type MerchantActivePartnerRow,
} from "@/lib/dashboard/merchantActivePartners";
import { scopeFinancialsByDate } from "./dateScopedData";

const r2 = (n: number) => Math.round(n * 100) / 100;

/** One roll-up line — the existing merchant row + its in-range invoiced total (passthrough). */
export interface MerchantStatementLine extends MerchantActivePartnerRow {
  /** Invoiced total in range = paidToDate + remaining (read-model passthrough, never a recompute). */
  billed: number;
}

export interface MerchantRollupStatement {
  tenantId: string;
  merchantName?: string;
  lineCount: number;
  lines: MerchantStatementLine[];
  /** Combined totals across all partner lines — a sum of the per-line read-model figures. */
  totals: { billed: number; paid: number; remaining: number };
}

/**
 * Assemble the merchant's cross-partner statement for the selected date range.
 * Single source: date-scope financials → existing dashboard builder → sum per-line figures.
 */
export function buildMerchantRollupStatement(
  data: PortalData,
  tenantId: string,
  partnerNameById: Map<string, string>,
  range: DateRange | undefined = undefined,
  now: Date = new Date(),
): MerchantRollupStatement {
  const scoped = scopeFinancialsByDate(data, range);
  const rows = buildMerchantActivePartners(scoped, tenantId, partnerNameById, now);

  const lines: MerchantStatementLine[] = rows.map((row) => ({
    ...row,
    billed: r2(row.payment.paidToDate + row.payment.remaining),
  }));

  const totals = lines.reduce(
    (acc, line) => ({
      billed: r2(acc.billed + line.billed),
      paid: r2(acc.paid + line.payment.paidToDate),
      remaining: r2(acc.remaining + line.payment.remaining),
    }),
    { billed: 0, paid: 0, remaining: 0 },
  );

  return {
    tenantId,
    merchantName: data.activations.find((a) => a.tenantId === tenantId)?.merchantName,
    lineCount: lines.length,
    lines,
    totals,
  };
}
