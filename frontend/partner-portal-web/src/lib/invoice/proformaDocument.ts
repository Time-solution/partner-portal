/**
 * Proforma document model (MOCK / BETA — NOT a ZATCA tax invoice).
 *
 * ONE document shape feeds BOTH the PDF and the Excel generators, so the two
 * outputs are always identical in content. This module is pure: it READS the
 * existing invoicing read models (SubscriptionBillingPeriod / deriveInvoicePayment
 * for the merchant invoice; PartnerStatement for the partner settlement statement)
 * and the admin OrgProfile, then MAPS them into the layout shape. It never
 * recomputes money — every figure is passed straight through from the read model.
 *
 * Direction 1 — INVOICE   : an entity OWES Zahy (receivable).
 * Direction 2 — STATEMENT  : Zahy OWES the partner (payable). Kept a SEPARATE document.
 */
import type { SubscriptionBillingPeriod } from "@/lib/data/types";
import { deriveInvoicePayment, invoiceTotal } from "@/lib/data/types";
import type { OrgProfile } from "@/lib/profile/orgProfile";
import type { PartnerStatement } from "@/lib/reports/settlementReports";
import { ReportAccount } from "@/lib/reports/settlementReports";
import type { Lang } from "@/lib/i18n";
import {
  rollupPartnerPayableVat,
  splitPartnerPayableInclusive,
} from "@/lib/statements/partnerPayableVat";

const r2 = (n: number) => Math.round(n * 100) / 100;

/** Bilingual literal helper for document chrome (kept local; not app i18n keys). */
export function L(lang: Lang, ar: string, en: string): string {
  return lang === "ar" ? ar : en;
}

export type ProformaKind = "invoice" | "statement";

/** The BETA proforma marks — single source for both PDF + Excel. NOT a tax invoice. */
export const PROFORMA_DOC_TYPE = "بيان / Statement (BETA / تجريبي)";
export const PROFORMA_BETA_NOTICE_AR =
  "مستند تجريبي (BETA) — ليس فاتورة ضريبية متوافقة مع هيئة الزكاة والضريبة والجمارك. الفاتورة الضريبية تصدر في مرحلة لاحقة.";
export const PROFORMA_BETA_NOTICE_EN =
  "BETA proforma — NOT a ZATCA-compliant tax invoice. The compliant tax invoice is issued in a later phase.";

export interface ProformaParty {
  name: string;
  vatNumber: string;
  crNumber: string;
  nationalNumber?: string;
  address: string;
  phone: string;
  email: string;
}

export interface ProformaLine {
  serviceType: string;
  billingType: string;
  /** Human basis text: "120 × 1.00" (per-txn) or "اشتراك 15/30 يوم" (prorated subscription). */
  basis: string;
  exVat: number;
  vat: number;
  inclusive: number;
}

export interface ProformaTotals {
  subtotalExVat: number;
  totalVat: number;
  grandTotalInclusive: number;
  paidToDate: number;
  remaining: number;
}

export interface ProformaDocument {
  kind: ProformaKind;
  docTypeLabel: string;
  docNumber: string;
  periodLabel: string;
  issueDate: string;
  currency: string;
  issuer: ProformaParty;
  billTo: ProformaParty;
  /** Merchant invoice only: "via {Partner}". */
  viaPartnerName?: string;
  /** Short service context shown under the bill-to (e.g. the per-partner service). */
  serviceContext?: string;
  lines: ProformaLine[];
  totals: ProformaTotals;
  lang: Lang;
}

function toParty(profile: OrgProfile, fallbackName?: string): ProformaParty {
  return {
    name: profile.name || fallbackName || "",
    vatNumber: profile.vatNumber,
    crNumber: profile.crNumber,
    nationalNumber: profile.nationalNumber,
    address: profile.address,
    phone: profile.contacts.phone,
    email: profile.contacts.email,
  };
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

export interface ProformaCoreInput {
  kind: ProformaKind;
  docNumber: string;
  periodLabel: string;
  issueDate?: string;
  currency: string;
  issuer: OrgProfile;
  billTo: OrgProfile;
  billToFallbackName?: string;
  viaPartnerName?: string;
  serviceContext?: string;
  lines: ProformaLine[];
  /** Totals from the read model — assembled, never recomputed here. */
  totals: ProformaTotals;
  lang: Lang;
}

/**
 * Assemble a {@link ProformaDocument} from already-computed pieces. PURE pass-through:
 * the lines and totals are taken verbatim from the caller's read-model figures.
 */
export function buildProforma(input: ProformaCoreInput): ProformaDocument {
  return {
    kind: input.kind,
    docTypeLabel: PROFORMA_DOC_TYPE,
    docNumber: input.docNumber,
    periodLabel: input.periodLabel,
    issueDate: input.issueDate ?? todayIso(),
    currency: input.currency,
    issuer: toParty(input.issuer),
    billTo: toParty(input.billTo, input.billToFallbackName),
    viaPartnerName: input.viaPartnerName,
    serviceContext: input.serviceContext,
    lines: input.lines,
    totals: input.totals,
    lang: input.lang,
  };
}

export interface InvoiceProformaInput {
  period: SubscriptionBillingPeriod;
  issuer: OrgProfile;
  billTo: OrgProfile;
  viaPartnerName?: string;
  lang: Lang;
  /** Optional extra service lines already shaped from read models (per-transaction etc.). */
  extraLines?: ProformaLine[];
  /** When extraLines are supplied, the read-model totals for the whole invoice. */
  totalsOverride?: ProformaTotals;
}

/**
 * Direction 1 — merchant/partner INVOICE (receivable). Reads the billing period's
 * own VAT-inclusive fee (feeInclusive), its ex-VAT (netFee) and VAT (outputVat), plus
 * the live payment state (paidToDate / remaining) from {@link deriveInvoicePayment}.
 * No money is recomputed — only mapped.
 */
export function buildInvoiceProforma(input: InvoiceProformaInput): ProformaDocument {
  const { period, lang } = input;
  const payment = deriveInvoicePayment(period);

  const subscriptionLine: ProformaLine = {
    serviceType: L(lang, "رسوم اشتراك المنصة", "Platform subscription fee"),
    billingType: L(lang, "اشتراك", "Subscription"),
    basis: period.periodKey,
    exVat: period.netFee,
    vat: period.outputVat,
    inclusive: invoiceTotal(period),
  };

  const lines = [subscriptionLine, ...(input.extraLines ?? [])];
  const totals: ProformaTotals = input.totalsOverride ?? {
    subtotalExVat: period.netFee,
    totalVat: period.outputVat,
    grandTotalInclusive: payment.total,
    paidToDate: payment.amountPaid,
    remaining: payment.amountOutstanding,
  };

  return buildProforma({
    kind: "invoice",
    docNumber: period.invoiceNumber ?? `ZH-${period.periodKey.replace("-", "")}-INV`,
    periodLabel: period.periodKey,
    currency: period.feeInclusive.currency,
    issuer: input.issuer,
    billTo: input.billTo,
    billToFallbackName: period.merchantName,
    viaPartnerName: input.viaPartnerName,
    serviceContext: L(lang, "اشتراك المنصة", "Platform subscription"),
    lines,
    totals,
    lang,
  });
}

export interface StatementProformaInput {
  statement: PartnerStatement;
  issuer: OrgProfile;
  billTo: OrgProfile;
  lang: Lang;
}

/** Net PartnerPayable contribution of one report entry (credit − debit). Reads journal lines. */
function entryPayable(lines: { account: string; direction: string; amount: { amount: number } }[]): number {
  let net = 0;
  for (const l of lines) {
    if (l.account !== ReportAccount.PartnerPayable) continue;
    net += l.direction === "Credit" ? l.amount.amount : -l.amount.amount;
  }
  return r2(net);
}

/**
 * Direction 2 — partner SETTLEMENT STATEMENT (payable: Zahy owes the partner). A SEPARATE
 * document from the invoice. Reads the PartnerStatement read model: the order-level lines
 * come from the entries' existing PartnerPayable journal lines, and the grand total is the
 * read model's own `payable`. No money is recomputed.
 */
export function buildStatementProforma(input: StatementProformaInput): ProformaDocument {
  const { statement, lang } = input;

  const lineSplits = statement.entries
    .filter((e) => e.financial)
    .map((e) => {
      const net = entryPayable(e.lines);
      return { entry: e, split: splitPartnerPayableInclusive(net) };
    })
    .filter(({ split }) => split.inclusive !== 0);

  const lines: ProformaLine[] = lineSplits.map(({ entry: e, split }) => ({
    serviceType: e.merchantName || e.orderRef,
    billingType: L(lang, "تسوية شريك", "Partner settlement"),
    basis: e.orderRef,
    exVat: split.exVat,
    vat: split.inputVat,
    inclusive: split.inclusive,
  }));

  const rolled = rollupPartnerPayableVat(
    lineSplits.map(({ split }) => split),
    statement.payable,
  );

  const periodLabel = String(statement.period);
  const totals: ProformaTotals = {
    subtotalExVat: rolled.exVat,
    totalVat: rolled.inputVat,
    grandTotalInclusive: rolled.inclusive,
    // Frontend mock has no disbursement ledger — nothing settled yet; remaining = full payable.
    paidToDate: 0,
    remaining: rolled.inclusive,
  };

  return buildProforma({
    kind: "statement",
    docNumber: `ZH-${periodLabel.replace("-", "")}-PSTMT-${statement.partnerId.slice(0, 8)}`,
    periodLabel,
    currency: "SAR",
    issuer: input.issuer,
    billTo: input.billTo,
    billToFallbackName: statement.partnerName,
    serviceContext: L(lang, "المبلغ المستحق للشريك", "Amount owed to partner"),
    lines,
    totals,
    lang,
  });
}
