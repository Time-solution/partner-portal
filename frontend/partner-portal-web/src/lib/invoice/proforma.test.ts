import { describe, expect, it } from "vitest";
import { RIYAL_SVG_PATHS } from "@/components/riyalSymbol";
import type { OrgProfile } from "@/lib/profile/orgProfile";
import type { SubscriptionBillingPeriod } from "@/lib/data/types";
import type { PartnerStatement } from "@/lib/reports/settlementReports";
import { ReportAccount } from "@/lib/reports/settlementReports";
import {
  buildInvoiceProforma,
  buildProforma,
  buildStatementProforma,
  type ProformaLine,
  type ProformaTotals,
} from "./proformaDocument";
import { buildProformaHtml, riyalMarkup } from "./proformaHtml";
import { buildProformaSheetMatrix } from "./proformaExcel";
import { moneyMarkup, MONEY_SYMBOL_GAP_EM } from "@/lib/format/moneyMarkup";

const issuer: OrgProfile = {
  name: "Zahy Platform KSA",
  vatNumber: "300000000000003",
  crNumber: "1010000000",
  address: "الرياض، المملكة العربية السعودية",
  contacts: { phone: "+966112345678", email: "billing@zahy.sa" },
};

const merchant: OrgProfile = {
  name: "Al Rajhi Demo Store",
  vatNumber: "310000000000099",
  crNumber: "1010999999",
  nationalNumber: "7000000099",
  address: "شارع التحلية، الرياض",
  contacts: { phone: "+966501234567", email: "merchant@zahy.dev" },
};

/** A 2-line invoice totalling 45.00 incl (39.13 ex + 5.87 vat) — mirrors the backend fixture. */
function twoLineInvoiceDoc() {
  const lines: ProformaLine[] = [
    { serviceType: "إعداد المتجر", billingType: "اشتراك", basis: "2026-06", exVat: 26.09, vat: 3.91, inclusive: 30.0 },
    { serviceType: "رسوم لكل عملية", billingType: "لكل عملية", basis: "15 × 1.00", exVat: 13.04, vat: 1.96, inclusive: 15.0 },
  ];
  const totals: ProformaTotals = {
    subtotalExVat: 39.13,
    totalVat: 5.87,
    grandTotalInclusive: 45.0,
    paidToDate: 20.0,
    remaining: 25.0,
  };
  return buildProforma({
    kind: "invoice",
    docNumber: "ZH-202606-M099-0001",
    periodLabel: "2026-06",
    issueDate: "2026-06-21",
    currency: "SAR",
    issuer,
    billTo: merchant,
    viaPartnerName: "Jahez",
    lang: "ar",
    lines,
    totals,
  });
}

describe("proforma PDF (HTML layout the PDF captures)", () => {
  it("generates a 2-line invoice document totalling 45.00 incl", () => {
    const html = buildProformaHtml(twoLineInvoiceDoc());
    expect(html).toContain("45.00");
    expect(html).toContain("39.13");
    expect(html).toContain("5.87");
    // both service lines present
    expect(html).toContain("إعداد المتجر");
    expect(html).toContain("رسوم لكل عملية");
  });

  it("renders the Riyal as a sharp inline SVG vector (not a tofu glyph)", () => {
    const html = buildProformaHtml(twoLineInvoiceDoc());
    expect(riyalMarkup()).toContain("<svg");
    expect(riyalMarkup()).toContain(RIYAL_SVG_PATHS[0].slice(0, 32));
    // the document body embeds the same vector on money cells
    expect(html).toContain(RIYAL_SVG_PATHS[0].slice(0, 32));
  });

  it("is a BETA proforma/statement — never a tax invoice, with watermark", () => {
    const html = buildProformaHtml(twoLineInvoiceDoc());
    expect(html).toContain("تجريبي");
    expect(html).toContain("BETA");
    expect(html).toContain("بيان / Statement");
    expect(html.toLowerCase()).not.toContain("tax invoice");
  });

  it("renders right-to-left for Arabic, with header / bill-to / lines / totals / footer", () => {
    const html = buildProformaHtml(twoLineInvoiceDoc());
    expect(html).toContain('dir="rtl"');
    expect(html).toContain('data-section="header"');
    expect(html).toContain('data-section="billto"');
    expect(html).toContain('data-section="lines"');
    expect(html).toContain('data-section="totals"');
    expect(html).toContain('data-section="footer"');
    // issuer + bill-to come from the profiles
    expect(html).toContain("Zahy Platform KSA");
    expect(html).toContain("Al Rajhi Demo Store");
    // merchant invoice shows "via {Partner}"
    expect(html).toContain("Jahez");
  });

  it("renders LTR for English", () => {
    const doc = { ...twoLineInvoiceDoc(), lang: "en" as const };
    expect(buildProformaHtml(doc)).toContain('dir="ltr"');
  });
});

describe("money layout — Riyal symbol sits LEFT of the number (SAMA), dir-locked", () => {
  it("shared moneyMarkup puts the symbol BEFORE the number with the one consistent gap", () => {
    const m = moneyMarkup(45);
    // symbol (svg) comes before the digits
    expect(m.indexOf("<svg")).toBeLessThan(m.indexOf("45.00"));
    // dir-locked so RTL cannot flip the symbol to the right
    expect(m).toContain('dir="ltr"');
    expect(m).toContain("flex-direction:row");
    // the single shared gap (0.2em)
    expect(m).toContain(`gap:${MONEY_SYMBOL_GAP_EM}em`);
    expect(MONEY_SYMBOL_GAP_EM).toBeGreaterThanOrEqual(0.15);
    expect(MONEY_SYMBOL_GAP_EM).toBeLessThanOrEqual(0.25);
  });

  it("proforma money cells consume the shared component — symbol left even in Arabic (RTL)", () => {
    const doc = twoLineInvoiceDoc(); // lang: "ar" → dir="rtl" document
    const html = buildProformaHtml(doc);
    // document is RTL, but each money group is the dir-locked LTR shared markup
    expect(html).toContain('dir="rtl"');
    expect(html).toContain(`dir="ltr" style="display:inline-flex;flex-direction:row`);
    expect(html).toContain(`gap:${MONEY_SYMBOL_GAP_EM}em`);
    // the money cell is exactly what the single source produced (symbol left, then number)
    expect(html).toContain(moneyMarkup(45));
    const cell = moneyMarkup(45);
    expect(cell.indexOf("<svg")).toBeLessThan(cell.indexOf("45.00"));
  });
});

describe("proforma Excel (formatted document, not a raw dump)", () => {
  it("emits header block, bill-to, line table and totals with the read-model money", () => {
    const matrix = buildProformaSheetMatrix(twoLineInvoiceDoc());
    const flat = matrix.map((r) => r.join("|")).join("\n");

    expect(flat).toContain("بيان / Statement (BETA / تجريبي)");
    expect(flat).toContain("Al Rajhi Demo Store");
    expect(flat).toContain("ريال");
    // line + totals money present as numeric cells
    const numbers = matrix.flat().filter((c): c is number => typeof c === "number");
    expect(numbers).toContain(45.0);
    expect(numbers).toContain(39.13);
    expect(numbers).toContain(5.87);
    expect(numbers).toContain(25.0); // remaining
  });
});

describe("Direction 1 — invoice maps the billing period read model (no recompute)", () => {
  const period: SubscriptionBillingPeriod = {
    id: "bp-1",
    partnerId: "22222222-2222-2222-2222-222222222004",
    tenantId: "11111111-1111-1111-1111-111111111099",
    merchantName: "Al Rajhi Demo Store",
    periodKey: "2026-06",
    feeInclusive: { amount: 45.0, currency: "SAR", vatInclusive: true },
    outputVat: 5.87,
    netFee: 39.13,
    billingChargeId: "chg-1",
    invoiceNumber: "ZH-202606-M099-0001",
    journalBalanced: true,
    status: "Invoiced",
    dueDate: "2026-07-15",
    payments: [{ id: "pay-1", amount: { amount: 20.0, currency: "SAR", vatInclusive: true }, date: "2026-06-25", method: "Transfer", reference: "WX-1" }],
  };

  it("passes line + totals straight from the read model", () => {
    const doc = buildInvoiceProforma({ period, issuer, billTo: merchant, viaPartnerName: "Jahez", lang: "ar" });
    expect(doc.kind).toBe("invoice");
    expect(doc.docNumber).toBe("ZH-202606-M099-0001");
    expect(doc.lines[0].exVat).toBe(39.13); // netFee
    expect(doc.lines[0].vat).toBe(5.87); // outputVat
    expect(doc.lines[0].inclusive).toBe(45.0); // feeInclusive
    expect(doc.totals.grandTotalInclusive).toBe(45.0);
    expect(doc.totals.paidToDate).toBe(20.0); // sum of payments — read model
    expect(doc.totals.remaining).toBe(25.0);
  });
});

describe("Direction 2 — partner settlement statement is a SEPARATE document (no recompute)", () => {
  const statement: PartnerStatement = {
    partnerId: "22222222-2222-2222-2222-222222222004",
    partnerName: "Jahez Integration Services",
    period: "2026-06",
    payable: 70.0,
    orderCount: 1,
    reflectionCount: 0,
    entries: [
      {
        orderRef: "ORD-1",
        partnerId: "22222222-2222-2222-2222-222222222004",
        partnerName: "Jahez Integration Services",
        merchantName: "Al Rajhi Demo Store",
        period: "2026-06",
        mode: "Principal",
        financial: true,
        lines: [
          { account: ReportAccount.PartnerPayable, direction: "Credit", amount: { amount: 70.0, currency: "SAR", vatInclusive: false }, entry: "Principal" },
        ],
      },
    ],
  };

  it("reads payable straight from the read model and splits VAT on the statement document", () => {
    const stmt = buildStatementProforma({ statement, issuer, billTo: issuer, lang: "ar" });
    const inv = twoLineInvoiceDoc();

    expect(stmt.kind).toBe("statement");
    expect(stmt.totals.grandTotalInclusive).toBe(70.0); // = payable, passthrough
    expect(stmt.totals.remaining).toBe(70.0); // nothing disbursed on the mock
    expect(stmt.totals.subtotalExVat).toBe(60.87);
    expect(stmt.totals.totalVat).toBe(9.13);
    expect(stmt.totals.totalVat).not.toBe(0);
    expect(stmt.lines[0].exVat).toBe(60.87);
    expect(stmt.lines[0].vat).toBe(9.13);
    expect(stmt.lines[0].inclusive).toBe(70.0);

    // distinct documents
    expect(stmt.kind).not.toBe(inv.kind);
    expect(stmt.docNumber).not.toBe(inv.docNumber);
    expect(stmt.docNumber).toContain("PSTMT");

    // statement is also a BETA proforma, never a tax invoice — PDF/HTML shows the VAT split
    const html = buildProformaHtml(stmt);
    expect(html).toContain("بيان / Statement");
    expect(html.toLowerCase()).not.toContain("tax invoice");
    expect(html).toContain(moneyMarkup(60.87));
    expect(html).toContain(moneyMarkup(9.13));
    expect(html).toContain(moneyMarkup(70));

    const matrix = buildProformaSheetMatrix(stmt);
    const numbers = matrix.flat().filter((c): c is number => typeof c === "number");
    expect(numbers).toContain(60.87);
    expect(numbers).toContain(9.13);
    expect(numbers).toContain(70.0);
    expect(stmt.totals.totalVat).not.toBe(0);
  });
});
