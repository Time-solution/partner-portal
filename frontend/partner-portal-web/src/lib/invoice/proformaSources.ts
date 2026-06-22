/**
 * Wires the invoicing read models to the admin OrgProfile store and produces a
 * {@link ProformaDocument}. This is the single seam the UI uses, so the export buttons stay
 * one-liners. Reads only — no money is computed here.
 */
import type { SubscriptionBillingPeriod } from "@/lib/data/types";
import type { PartnerStatement } from "@/lib/reports/settlementReports";
import type { ContributionStatement } from "@/lib/reports/contributionStatement";
import { readOrgProfile } from "@/lib/profile/orgProfileStore";
import type { Lang } from "@/lib/i18n";
import {
  buildInvoiceProforma,
  buildProforma,
  buildStatementProforma,
  L,
  type ProformaDocument,
  type ProformaLine,
} from "./proformaDocument";

const PLATFORM_SCOPE = { kind: "platform", id: "zahy" } as const;

/** Direction 1 — a merchant subscription INVOICE (receivable) for one billing period. */
export function invoiceProformaFromPeriod(
  period: SubscriptionBillingPeriod,
  lang: Lang,
): ProformaDocument {
  const issuer = readOrgProfile(PLATFORM_SCOPE);
  const billTo = period.tenantId
    ? readOrgProfile({ kind: "merchant", id: period.tenantId })
    : readOrgProfile({ kind: "merchant", id: "" });
  const partner = readOrgProfile({ kind: "partner", id: period.partnerId });

  return buildInvoiceProforma({
    period,
    issuer,
    billTo,
    viaPartnerName: partner.name || undefined,
    lang,
  });
}

/** Direction 2 — a partner SETTLEMENT STATEMENT (payable). A separate document. */
export function statementProformaFromPartner(
  statement: PartnerStatement,
  lang: Lang,
): ProformaDocument {
  const issuer = readOrgProfile(PLATFORM_SCOPE);
  const billTo = readOrgProfile({ kind: "partner", id: statement.partnerId });
  return buildStatementProforma({ statement, issuer, billTo, lang });
}

/**
 * Platform CONTRIBUTION / P&L (BETA) — an internal management statement. Reuses the proforma
 * pipeline (PDF + Excel) verbatim: P&L rows become lines (cost as a negative), gross contribution
 * is the ex-VAT subtotal, net VAT (period) the VAT total, and net contribution the grand total.
 * No money recomputed — the figures are passed straight from {@link ContributionStatement}.
 */
export function contributionProformaFromStatement(
  c: ContributionStatement,
  periodLabel: string,
  lang: Lang,
): ProformaDocument {
  const issuer = readOrgProfile(PLATFORM_SCOPE);
  const lines: ProformaLine[] = [
    {
      serviceType: L(lang, "إيراد إعادة البيع (٤١٠٠)", "Resale revenue (4100)"),
      billingType: L(lang, "مبيعات", "Sales"),
      basis: periodLabel,
      exVat: c.salesResale,
      vat: 0,
      inclusive: c.salesResale,
    },
    {
      serviceType: L(lang, "إيراد الرسوم (٤٢٠٠)", "Fee revenue (4200)"),
      billingType: L(lang, "مبيعات", "Sales"),
      basis: periodLabel,
      exVat: c.salesFee,
      vat: 0,
      inclusive: c.salesFee,
    },
    {
      serviceType: L(lang, "تكلفة المبيعات (٥١٠٠)", "Cost of sales (5100)"),
      billingType: L(lang, "مشتريات", "Purchase"),
      basis: periodLabel,
      exVat: -c.costOfSales,
      vat: 0,
      inclusive: -c.costOfSales,
    },
  ];

  return buildProforma({
    kind: "statement",
    docNumber: `ZH-${periodLabel.replace(/[^0-9]/g, "").slice(0, 8) || "RANGE"}-PNL`,
    periodLabel,
    currency: "SAR",
    issuer,
    // Internal statement — issued by AND for the platform/management (no external bill-to).
    billTo: issuer,
    billToFallbackName: L(lang, "الإدارة (داخلي)", "Management (internal)"),
    serviceContext: L(lang, "بيان المساهمة / الأرباح (تجريبي)", "Contribution / P&L (BETA)"),
    lines,
    totals: {
      subtotalExVat: c.grossContribution,
      totalVat: c.netVatToZatca,
      grandTotalInclusive: c.netContribution,
      paidToDate: 0,
      remaining: c.netContribution,
    },
    lang,
  });
}
