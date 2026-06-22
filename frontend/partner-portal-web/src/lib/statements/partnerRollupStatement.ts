/**
 * PARTNER STATEMENT — a PURCHASE/payable-side statement for one partner (Direction 2).
 *
 * The partner's related statement: their active merchants + what Zahy owes them
 * (owed / disbursed / remaining), for the selected date range. It assembles the EXISTING partner
 * dashboard read model ({@link buildPartnerActiveMerchants}) on date-scoped data — no second
 * settlement math, no recompute. `disbursed` stays 0 (mock has no disbursement ledger).
 *
 * Scope: partner = PURCHASE/payable only. The row shape already excludes sell/resale/margin/
 * receivable (see PARTNER_ACTIVE_MERCHANT_FORBIDDEN_KEYS + assertPartnerRowFieldScope) — those
 * fields stay ABSENT here too, satisfying the partner finance-side scope without a toggle.
 */
import { type DateRange } from "@/lib/filters/dateRange";
import type { PortalData } from "@/lib/data/types";
import {
  buildPartnerActiveMerchants,
  type PartnerActiveMerchantRow,
} from "@/lib/dashboard/partnerActiveMerchants";
import { scopeFinancialsByDate } from "./dateScopedData";

const r2 = (n: number) => Math.round(n * 100) / 100;

export interface PartnerRollupStatement {
  partnerId: string;
  partnerName: string;
  lineCount: number;
  lines: PartnerActiveMerchantRow[];
  /** Combined Direction-2 totals — owed ties to partnerStatement.payable; disbursed=0 in mock. */
  totals: { owed: number; disbursed: number; remaining: number };
}

/**
 * Assemble the partner's settlement statement for the selected date range.
 * Single source: date-scope financials → existing dashboard builder; owed = partnerPayableTotal.
 */
export function buildPartnerRollupStatement(
  data: PortalData,
  partnerId: string,
  range: DateRange | undefined = undefined,
): PartnerRollupStatement {
  const scoped = scopeFinancialsByDate(data, range);
  const view = buildPartnerActiveMerchants(scoped, partnerId);

  const disbursed = view.rows.reduce((sum, row) => r2(sum + row.payable.disbursed), 0);
  const owed = view.partnerPayableTotal;

  return {
    partnerId: view.partnerId,
    partnerName: view.partnerName,
    lineCount: view.rows.length,
    lines: view.rows,
    totals: { owed, disbursed, remaining: r2(owed - disbursed) },
  };
}
