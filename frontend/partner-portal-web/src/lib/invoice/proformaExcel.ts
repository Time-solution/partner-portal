/**
 * Proforma → Excel (.xlsx via SheetJS). A FORMATTED single-document statement (header block,
 * bill-to, line-item table, totals) — NOT a raw row dump. Built from the SAME
 * {@link ProformaDocument} that feeds the PDF, so the two outputs match. The pure matrix
 * builder is separated from the browser download so it can be unit-tested. Money columns are
 * marked with the Arabic word "ريال" (no legacy SAR/﷼ marks) and emitted as numeric cells.
 */
import { L, type ProformaDocument } from "./proformaDocument";

type Cell = string | number;
type Matrix = Cell[][];

function riyalCol(lang: Lang, ar: string, en: string): string {
  return `${L(lang, ar, en)} (${L(lang, "ريال", "Riyal")})`;
}

type Lang = ProformaDocument["lang"];

/**
 * Build the formatted sheet as an array-of-arrays (header block → bill-to → line table →
 * totals → BETA notice). Pure: every money value is read straight from the document.
 */
export function buildProformaSheetMatrix(doc: ProformaDocument): Matrix {
  const lang = doc.lang;
  const rows: Matrix = [];
  const blank = () => rows.push([""]);

  // ---- BETA banner ----
  rows.push([doc.docTypeLabel]);
  blank();

  // ---- Issuer ----
  rows.push([L(lang, "الجهة المُصدِرة", "ISSUER")]);
  rows.push([L(lang, "الاسم", "Name"), doc.issuer.name]);
  rows.push([L(lang, "الرقم الضريبي", "VAT no."), doc.issuer.vatNumber]);
  rows.push([L(lang, "السجل التجاري", "CR no."), doc.issuer.crNumber]);
  rows.push([L(lang, "العنوان", "Address"), doc.issuer.address]);
  rows.push([L(lang, "هاتف", "Phone"), doc.issuer.phone, L(lang, "بريد", "Email"), doc.issuer.email]);
  blank();

  // ---- Document meta ----
  rows.push([L(lang, "رقم المستند", "Document no."), doc.docNumber]);
  rows.push([L(lang, "الفترة", "Period"), doc.periodLabel]);
  rows.push([L(lang, "تاريخ الإصدار", "Issue date"), doc.issueDate]);
  blank();

  // ---- Bill to ----
  rows.push([
    doc.kind === "invoice"
      ? L(lang, "فاتورة إلى (التاجر/الجهة)", "BILL TO (Merchant/Entity)")
      : L(lang, "بيان إلى (الشريك)", "STATEMENT TO (Partner)"),
  ]);
  rows.push([L(lang, "الاسم", "Name"), doc.billTo.name]);
  if (doc.kind === "invoice" && doc.viaPartnerName) {
    rows.push([L(lang, "عبر", "Via"), doc.viaPartnerName]);
  }
  rows.push([L(lang, "الرقم الضريبي", "VAT no."), doc.billTo.vatNumber]);
  rows.push([L(lang, "السجل التجاري", "CR no."), doc.billTo.crNumber]);
  if (doc.billTo.nationalNumber) {
    rows.push([L(lang, "الرقم الوطني", "National no."), doc.billTo.nationalNumber]);
  }
  rows.push([L(lang, "العنوان", "Address"), doc.billTo.address]);
  rows.push([L(lang, "هاتف", "Phone"), doc.billTo.phone, L(lang, "بريد", "Email"), doc.billTo.email]);
  if (doc.serviceContext) rows.push([L(lang, "نوع الخدمة", "Service"), doc.serviceContext]);
  blank();

  // ---- Line-item table ----
  rows.push([
    L(lang, "الخدمة", "Service"),
    L(lang, "نوع الفوترة", "Billing"),
    L(lang, "الأساس", "Basis"),
    riyalCol(lang, "قبل الضريبة", "Ex-VAT"),
    riyalCol(lang, "الضريبة", "VAT"),
    riyalCol(lang, "شامل الضريبة", "Incl. VAT"),
  ]);
  for (const l of doc.lines) {
    rows.push([l.serviceType, l.billingType, l.basis, l.exVat, l.vat, l.inclusive]);
  }
  blank();

  // ---- Totals ----
  const t = doc.totals;
  rows.push([riyalCol(lang, "الإجمالي قبل الضريبة", "Subtotal (ex-VAT)"), t.subtotalExVat]);
  rows.push([riyalCol(lang, "إجمالي الضريبة", "Total VAT"), t.totalVat]);
  rows.push([riyalCol(lang, "الإجمالي شامل الضريبة", "GRAND TOTAL (incl.)"), t.grandTotalInclusive]);
  rows.push([riyalCol(lang, "المسدد حتى تاريخه", "Paid to date"), t.paidToDate]);
  rows.push([riyalCol(lang, "المتبقي", "Remaining"), t.remaining]);
  blank();

  // ---- BETA notice ----
  rows.push([
    L(
      lang,
      "مستند تجريبي (BETA) — ليس فاتورة ضريبية متوافقة مع هيئة الزكاة والضريبة والجمارك.",
      "BETA proforma — NOT a ZATCA-compliant tax invoice.",
    ),
  ]);

  return rows;
}

/** Apply a 2-decimal money format to numeric cells (matches the other exports). */
function applyMoneyFormat(XLSX: typeof import("xlsx"), ws: import("xlsx").WorkSheet): void {
  const ref = ws["!ref"];
  if (!ref) return;
  const range = XLSX.utils.decode_range(ref);
  for (let r = range.s.r; r <= range.e.r; r++) {
    for (let c = range.s.c; c <= range.e.c; c++) {
      const cell = ws[XLSX.utils.encode_cell({ r, c })];
      if (cell && cell.t === "n") cell.z = "0.00";
    }
  }
}

/** Build the formatted proforma workbook and trigger a browser download. */
export async function downloadProformaXlsx(doc: ProformaDocument): Promise<void> {
  const XLSX = await import("xlsx");
  const matrix = buildProformaSheetMatrix(doc);
  const ws = XLSX.utils.aoa_to_sheet(matrix);
  ws["!cols"] = [{ wch: 28 }, { wch: 22 }, { wch: 18 }, { wch: 14 }, { wch: 12 }, { wch: 14 }];
  if (doc.lang === "ar") ws["!sheetViews"] = [{ RTL: true }];
  applyMoneyFormat(XLSX, ws);

  const wb = XLSX.utils.book_new();
  const sheetName = (doc.kind === "invoice" ? "Invoice" : "Statement").slice(0, 31);
  XLSX.utils.book_append_sheet(wb, ws, sheetName);
  XLSX.writeFile(wb, `${doc.docNumber}.xlsx`);
}
