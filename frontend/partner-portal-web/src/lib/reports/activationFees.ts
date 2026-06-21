/**
 * Activation fee matrix + per-partner bulk invoice — DISPLAY ONLY (mock).
 *
 * No journal is ever posted from these figures (the fee journal stays OFF in the
 * backend). The UI uses `feeBreakdown` to preview a VAT-inclusive fee as ex-VAT + VAT,
 * and `buildPartnerBulkInvoice` to roll up a partner's successful ReflectionOnly
 * transactions across all its merchants into one monthly statement.
 */
import type { ActivationFeeConfig, ReflectedPartnerOrder } from "@/lib/data/types";
import { VAT_RATE, periodOf, splitInclusiveVat } from "./settlementReports";

const r2 = (n: number) => Math.round(n * 100) / 100;

export interface FeeBreakdown {
  inclusive: number;
  net: number;
  vat: number;
}

/** Split a VAT-inclusive amount into ex-VAT + VAT (e.g. 1.00 incl → 0.87 + 0.13). */
export function feeBreakdown(inclusive: number, vatRate = VAT_RATE): FeeBreakdown {
  const { exVat, vat } = splitInclusiveVat(inclusive, vatRate);
  return { inclusive: r2(inclusive), net: exVat, vat };
}

/** Sum of the enabled monthly + per-txn fee lines on an activation (display only). */
export function activeFeeLines(fees?: ActivationFeeConfig) {
  if (!fees) return [];
  const lines: { kind: "subscription" | "perTransaction"; amount: number; payer: string }[] = [];
  if (fees.subscription.enabled)
    lines.push({ kind: "subscription", amount: fees.subscription.amountInclusive.amount, payer: fees.subscription.payer });
  if (fees.perTransaction.enabled)
    lines.push({ kind: "perTransaction", amount: fees.perTransaction.amountInclusive.amount, payer: fees.perTransaction.payer });
  return lines;
}

export interface BulkInvoiceTxn {
  orderRef: string;
  feeInclusive: number;
}

export interface BulkInvoiceLine {
  tenantId: string;
  merchantName: string;
  transactions: BulkInvoiceTxn[];
  subtotalInclusive: number;
}

export interface BulkInvoice {
  partnerId: string;
  partnerName: string;
  period: string;
  lines: BulkInvoiceLine[];
  transactionCount: number;
  totalInclusive: number;
  totalNet: number;
  totalVat: number;
}

/**
 * Build a partner's monthly bulk invoice: every successful ReflectionOnly transaction
 * in the period (across ALL the partner's merchants), at its per-txn fee (1 SR incl by
 * default), summed into one total with a per-merchant / per-transaction drill-down and
 * an ex-VAT + VAT split on the total.
 */
export function buildPartnerBulkInvoice(
  orders: ReflectedPartnerOrder[],
  partnerId: string,
  period: string,
  vatRate = VAT_RATE,
): BulkInvoice {
  const own = orders.filter(
    (o) =>
      o.partnerId === partnerId &&
      periodOf(o.reflectedAt) === period &&
      o.reflection?.successful,
  );

  const byMerchant = new Map<string, BulkInvoiceLine>();
  for (const o of own) {
    const fee = o.reflection?.zahyFeeInclusive?.amount ?? 1;
    const line = byMerchant.get(o.tenantId) ?? {
      tenantId: o.tenantId,
      merchantName: o.merchantName,
      transactions: [],
      subtotalInclusive: 0,
    };
    line.transactions.push({ orderRef: o.externalTransactionId, feeInclusive: fee });
    line.subtotalInclusive = r2(line.subtotalInclusive + fee);
    byMerchant.set(o.tenantId, line);
  }

  const lines = Array.from(byMerchant.values()).sort((a, b) => a.merchantName.localeCompare(b.merchantName));
  const totalInclusive = r2(lines.reduce((s, l) => s + l.subtotalInclusive, 0));
  const split = feeBreakdown(totalInclusive, vatRate);

  return {
    partnerId,
    partnerName: own[0]?.partnerName ?? "",
    period,
    lines,
    transactionCount: own.length,
    totalInclusive,
    totalNet: split.net,
    totalVat: split.vat,
  };
}
