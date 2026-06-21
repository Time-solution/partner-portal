import type { MerchantActivation, PortalData } from "@/lib/data/types";
import {
  partnerStatement,
  ReportAccount,
  toReportEntries,
  type ReportEntry,
} from "@/lib/reports/settlementReports";

const r2 = (n: number) => Math.round(n * 100) / 100;

/** Partner payable side for one merchant — filtered slice of PartnerStatement entries. */
export interface PartnerMerchantPayableSide {
  /** Net PartnerPayable (2100 credit − debit) for this merchant's activity. */
  owed: number;
  /** Disbursement ledger OFF in mock — compute/record only until sign-off. */
  disbursed: number;
  /** owed − disbursed (Direction-2 remainingToDisburse semantics). */
  remainingToDisburse: number;
}

/** One row on the partner "Active Merchants" dashboard — partner buy/payable side ONLY. */
export interface PartnerActiveMerchantRow {
  activationId: string;
  tenantId: string;
  merchantName: string;
  tierName: string;
  tierCode: string;
  catalogItemId: string;
  status: MerchantActivation["status"];
  activeSince?: string;
  payable: PartnerMerchantPayableSide;
}

export interface PartnerActiveMerchantsView {
  partnerId: string;
  partnerName: string;
  /** Tie-out to partnerStatement.payable for the scoped dataset. */
  partnerPayableTotal: number;
  rows: PartnerActiveMerchantRow[];
}

/** Keys that must be absent from partner dashboard rows (not CSS-hidden). */
export const PARTNER_ACTIVE_MERCHANT_FORBIDDEN_KEYS = [
  "sellPrice",
  "resalePrice",
  "margin",
  "merchantReceivable",
  "receivable",
] as const;

/**
 * Sum PartnerPayable net for one merchant from existing report entries — filter only,
 * no journal rebuild (same line walk as statement proforma).
 */
export function sumMerchantPartnerPayable(entries: ReportEntry[], tenantId: string): number {
  let total = 0;
  for (const e of entries) {
    if (e.tenantId !== tenantId || !e.financial) continue;
    for (const l of e.lines) {
      if (l.account !== ReportAccount.PartnerPayable) continue;
      total += l.direction === "Credit" ? l.amount.amount : -l.amount.amount;
    }
  }
  return r2(total);
}

/**
 * Scoped partner dashboard — active merchant activations + payable side per merchant.
 * Single source: toReportEntries → partnerStatement + activation list filter.
 */
export function buildPartnerActiveMerchants(
  data: PortalData,
  partnerId: string,
): PartnerActiveMerchantsView {
  const entries = toReportEntries(data);
  const statement = partnerStatement(entries, partnerId);
  const partner = data.partners.find((p) => p.id === partnerId);
  const partnerName = partner?.tradeName ?? partner?.legalName ?? statement.partnerName;

  const rows: PartnerActiveMerchantRow[] = data.activations
    .filter((a) => a.partnerId === partnerId && a.status === "Active")
    .sort((a, b) => a.merchantName.localeCompare(b.merchantName))
    .map((a) => {
      const owed = sumMerchantPartnerPayable(statement.entries, a.tenantId);
      const disbursed = 0;
      return {
        activationId: a.id,
        tenantId: a.tenantId,
        merchantName: a.merchantName,
        tierName: a.catalogItemName,
        tierCode: data.catalogItems.find((c) => c.id === a.catalogItemId)?.code ?? "",
        catalogItemId: a.catalogItemId,
        status: a.status,
        activeSince: a.activatedAt,
        payable: {
          owed,
          disbursed,
          remainingToDisburse: r2(owed - disbursed),
        },
      };
    });

  return {
    partnerId,
    partnerName,
    partnerPayableTotal: statement.payable,
    rows,
  };
}

/** Assert partner row shape excludes merchant sell / margin fields. */
export function assertPartnerRowFieldScope(row: PartnerActiveMerchantRow): void {
  const raw = row as unknown as Record<string, unknown>;
  for (const key of PARTNER_ACTIVE_MERCHANT_FORBIDDEN_KEYS) {
    if (key in raw) {
      throw new Error(`Forbidden field "${key}" present on partner active merchant row`);
    }
  }
}
