/**
 * Money-cycle export (MOCK, frontend only) — Excel (.xlsx via SheetJS) + CSV.
 *
 * Pure row builders are separated from the browser download side-effects so they can be
 * unit-tested. Content = INVOICES (with payment status / paid / outstanding) + a RECEIPTS sheet.
 * Headers follow the active language; the export reflects whatever scoped/filtered records are
 * passed in (admin/accountant see all within scope).
 */
import type { Receipt, SubscriptionBillingPeriod } from "@/lib/data/types";
import { deriveInvoicePayment, invoiceTotal } from "@/lib/data/types";
import { translations, type Lang } from "@/lib/i18n";
import { formatOrderRef } from "@/lib/format/recordId";

export type ExportRow = Record<string, string | number>;

export interface MoneyExportInput {
  invoices: SubscriptionBillingPeriod[];
  receipts: Receipt[];
  lang: Lang;
}

function t(lang: Lang, key: string): string {
  return translations[lang][key] ?? key;
}

function money(n: number): number {
  return Math.round(n * 100) / 100;
}

function formatDate(iso: string | undefined, lang: Lang): string {
  if (!iso) return "";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString(lang === "ar" ? "ar-SA" : "en-GB");
}

export function paymentStatusLabel(lang: Lang, status: string): string {
  return t(lang, `payStatus_${status}`);
}

export function paymentMethodLabel(lang: Lang, method: string): string {
  return t(lang, `payMethod_${method}`);
}

function invoiceHeaders(lang: Lang) {
  return {
    period: t(lang, "colPeriod"),
    merchant: t(lang, "colMerchant"),
    invoice: t(lang, "colInvoice"),
    total: t(lang, "colInvoiced"),
    paid: t(lang, "colPaid"),
    outstanding: t(lang, "colOutstanding"),
    payment: t(lang, "colPayment"),
    due: t(lang, "colDue"),
    currency: t(lang, "colCurrency"),
  };
}

function receiptHeaders(lang: Lang) {
  return {
    receiptNo: t(lang, "receiptNo"),
    invoice: t(lang, "colInvoice"),
    merchant: t(lang, "colMerchant"),
    amount: t(lang, "colPaid"),
    date: t(lang, "colDate"),
    method: t(lang, "paymentMethod"),
    sent: t(lang, "colStatus"),
    currency: t(lang, "colCurrency"),
  };
}

/** One row per invoice — payment status / paid / outstanding. */
export function buildInvoiceRows(input: MoneyExportInput): ExportRow[] {
  const { invoices, lang } = input;
  const h = invoiceHeaders(lang);
  return invoices.map((p) => {
    const state = deriveInvoicePayment(p);
    return {
      [h.period]: p.periodKey,
      [h.merchant]: p.merchantName,
      [h.invoice]: p.invoiceNumber ?? "—",
      [h.total]: money(invoiceTotal(p)),
      [h.paid]: money(state.amountPaid),
      [h.outstanding]: money(state.amountOutstanding),
      [h.payment]: paymentStatusLabel(lang, state.status),
      [h.due]: formatDate(p.dueDate, lang),
      [h.currency]: p.feeInclusive.currency,
    };
  });
}

/** One row per receipt — the proof-of-payment sheet. */
export function buildReceiptRows(input: MoneyExportInput): ExportRow[] {
  const { receipts, lang } = input;
  const h = receiptHeaders(lang);
  return receipts.map((r) => ({
    [h.receiptNo]: r.receiptNo,
    [h.invoice]: formatOrderRef(r.invoiceRef, lang),
    [h.merchant]: r.merchantName,
    [h.amount]: money(r.amount.amount),
    [h.date]: formatDate(r.date, lang),
    [h.method]: paymentMethodLabel(lang, r.method),
    [h.sent]: r.sent ? t(lang, "receiptSent") : "—",
    [h.currency]: r.amount.currency,
  }));
}

function csvCell(value: string | number): string {
  if (typeof value === "number") return value.toFixed(2);
  const needsQuote = /[",\r\n]/.test(value);
  const escaped = value.replace(/"/g, '""');
  return needsQuote ? `"${escaped}"` : escaped;
}

export function rowsToCsv(rows: ExportRow[]): string {
  if (rows.length === 0) return "";
  const headers = Object.keys(rows[0]);
  const lines = [headers.map(csvCell).join(",")];
  for (const row of rows) {
    lines.push(headers.map((key) => csvCell(row[key] ?? "")).join(","));
  }
  return lines.join("\r\n");
}

/** Combined CSV: invoices block, blank line, then receipts block. */
export function buildMoneyCsv(input: MoneyExportInput): string {
  const invoices = rowsToCsv(buildInvoiceRows(input));
  const receipts = rowsToCsv(buildReceiptRows(input));
  return receipts ? `${invoices}\r\n\r\n${receipts}` : invoices;
}

function timestamp(): string {
  return new Date().toISOString().slice(0, 10);
}

function triggerDownload(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

/** Download a CSV (UTF-8 BOM so Excel renders Arabic correctly). */
export function downloadMoneyCsv(input: MoneyExportInput): void {
  const csv = buildMoneyCsv(input);
  const blob = new Blob([`\uFEFF${csv}`], { type: "text/csv;charset=utf-8;" });
  triggerDownload(blob, `money-${timestamp()}.csv`);
}

function applyMoneyFormat(XLSX: typeof import("xlsx"), ws: import("xlsx").WorkSheet): void {
  const ref = ws["!ref"];
  if (!ref) return;
  const range = XLSX.utils.decode_range(ref);
  for (let r = range.s.r + 1; r <= range.e.r; r++) {
    for (let col = range.s.c; col <= range.e.c; col++) {
      const cell = ws[XLSX.utils.encode_cell({ r, c: col })];
      if (cell && cell.t === "n") cell.z = "0.00";
    }
  }
}

/** Download a two-sheet .xlsx (Invoices + Receipts) via SheetJS. */
export async function downloadMoneyXlsx(input: MoneyExportInput): Promise<void> {
  const XLSX = await import("xlsx");
  const { lang } = input;
  const wb = XLSX.utils.book_new();
  const wsInvoices = XLSX.utils.json_to_sheet(buildInvoiceRows(input));
  const wsReceipts = XLSX.utils.json_to_sheet(buildReceiptRows(input));
  applyMoneyFormat(XLSX, wsInvoices);
  applyMoneyFormat(XLSX, wsReceipts);
  XLSX.utils.book_append_sheet(wb, wsInvoices, t(lang, "exportInvoices").slice(0, 31));
  XLSX.utils.book_append_sheet(wb, wsReceipts, t(lang, "exportReceipts").slice(0, 31));
  XLSX.writeFile(wb, `money-${timestamp()}.xlsx`);
}
