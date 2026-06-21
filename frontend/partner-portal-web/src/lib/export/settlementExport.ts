/**
 * Settlement export (MOCK, frontend only) — Excel (.xlsx via SheetJS) + CSV.
 *
 * Pure row builders are separated from the browser side-effects (file download) so they can be
 * unit-tested. Content = settlement CASES (summary) + their JOURNAL LINES (double-entry), with
 * debits / credits / VAT / margin. Headers follow the active language; the export reflects whatever
 * scoped/filtered cases are passed in (admin sees all; accountant sees money scope).
 */
import type { SettlementCase } from "@/lib/data/types";
import { deriveSettlementSummary } from "@/lib/data/types";
import { translations, type Lang } from "@/lib/i18n";
import {
  journalDirectionLabel,
  settlementAccountLabel,
  settlementStateLabel,
} from "@/lib/i18n/domainLabels";
import { formatOrderRef } from "@/lib/format/recordId";

export type ExportRow = Record<string, string | number>;

export interface SettlementExportInput {
  cases: SettlementCase[];
  /** Resolve a human merchant name from the case tenantId (derived from activations). */
  merchantName: (tenantId?: string) => string;
  lang: Lang;
}

function t(lang: Lang, key: string): string {
  return translations[lang][key] ?? key;
}

function money(n: number): number {
  return Math.round(n * 100) / 100;
}

function formatDate(iso: string, lang: Lang): string {
  if (!iso) return "";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString(lang === "ar" ? "ar-SA" : "en-GB");
}

/** Localized column headers (single source for both sheets + CSV). */
export function exportHeaders(lang: Lang) {
  return {
    order: t(lang, "colOrder"),
    partner: t(lang, "colPartner"),
    merchant: t(lang, "colMerchant"),
    date: t(lang, "colDate"),
    status: t(lang, "colStatus"),
    book: t(lang, "colBook"),
    buy: t(lang, "settlementBuy"),
    sell: t(lang, "settlementSell"),
    margin: t(lang, "settlementMargin"),
    vatOutput: t(lang, "settlementVatOutput"),
    vatInput: t(lang, "settlementVatInput"),
    netVat: t(lang, "settlementNetVat"),
    currency: t(lang, "colCurrency"),
    account: t(lang, "journalColAccount"),
    direction: t(lang, "journalColDirection"),
    debit: t(lang, "journalColDebit"),
    credit: t(lang, "journalColCredit"),
    paymentMethod: t(lang, "colPaymentMethod"),
    totalCollected: t(lang, "colTotalCollected"),
    deductions: t(lang, "colDeductions"),
    netRemitted: t(lang, "colNetRemitted"),
    splitMerchant: t(lang, "colSplitMerchant"),
    splitDelivery: t(lang, "colSplitDelivery"),
    splitZahy: t(lang, "colSplitZahy"),
    splitZatca: t(lang, "colSplitZatca"),
  };
}

/** Collection/remittance export cells for a case (blank/0 when no collection). */
function collectionCells(c: SettlementCase, lang: Lang) {
  const h = exportHeaders(lang);
  const col = c.collection;
  return {
    [h.paymentMethod]: col ? t(lang, `payMethod_${col.paymentMethod}`) : "—",
    [h.totalCollected]: col ? money(col.totalCollected.amount) : 0,
    [h.deductions]: col ? money(col.deductions.reduce((s, d) => s + d.amount.amount, 0)) : 0,
    [h.netRemitted]: col ? money(col.netRemitted.amount) : 0,
    [h.splitMerchant]: col ? money(col.split.merchant.amount) : 0,
    [h.splitDelivery]: col ? money(col.split.deliveryCompany.amount) : 0,
    [h.splitZahy]: col ? money(col.split.zahy.amount) : 0,
    [h.splitZatca]: col ? money(col.split.zatca.amount) : 0,
  } satisfies ExportRow;
}

/** One row per settlement case — the accountant summary. */
export function buildCaseRows(input: SettlementExportInput): ExportRow[] {
  const { cases, merchantName, lang } = input;
  const h = exportHeaders(lang);
  return cases.map((c) => {
    const s = deriveSettlementSummary(c.journal);
    return {
      [h.order]: formatOrderRef(c.externalTransactionId, lang),
      [h.partner]: c.partnerName,
      [h.merchant]: merchantName(c.tenantId),
      [h.date]: formatDate(c.createdAt, lang),
      [h.status]: settlementStateLabel(lang, c.state),
      [h.book]: c.book,
      [h.buy]: money(s.buyPrice.amount),
      [h.sell]: money(s.sellPrice.amount),
      [h.margin]: money(s.margin),
      [h.vatOutput]: money(s.outputVat),
      [h.vatInput]: money(s.inputVat),
      [h.netVat]: money(s.netVatToZatca),
      ...collectionCells(c, lang),
      [h.currency]: c.journal.currency,
    };
  });
}

/** One row per journal line — double-entry debit/credit detail. */
export function buildJournalLineRows(input: SettlementExportInput): ExportRow[] {
  const { cases, merchantName, lang } = input;
  const h = exportHeaders(lang);
  const rows: ExportRow[] = [];
  for (const c of cases) {
    const order = formatOrderRef(c.externalTransactionId, lang);
    const merchant = merchantName(c.tenantId);
    for (const line of c.journal.lines) {
      const isDebit = line.direction === "Debit";
      rows.push({
        [h.order]: order,
        [h.partner]: c.partnerName,
        [h.merchant]: merchant,
        [h.account]: settlementAccountLabel(lang, line.account),
        [h.direction]: journalDirectionLabel(lang, line.direction),
        [h.debit]: isDebit ? money(line.amount.amount) : 0,
        [h.credit]: isDebit ? 0 : money(line.amount.amount),
        [h.currency]: line.amount.currency,
      });
    }
  }
  return rows;
}

/** Flat CSV — each row = one journal line with its case context (cases + journal lines combined). */
export function buildFlatRows(input: SettlementExportInput): ExportRow[] {
  const { cases, merchantName, lang } = input;
  const h = exportHeaders(lang);
  const rows: ExportRow[] = [];
  for (const c of cases) {
    const s = deriveSettlementSummary(c.journal);
    const order = formatOrderRef(c.externalTransactionId, lang);
    const merchant = merchantName(c.tenantId);
    for (const line of c.journal.lines) {
      const isDebit = line.direction === "Debit";
      rows.push({
        [h.order]: order,
        [h.partner]: c.partnerName,
        [h.merchant]: merchant,
        [h.date]: formatDate(c.createdAt, lang),
        [h.status]: settlementStateLabel(lang, c.state),
        [h.account]: settlementAccountLabel(lang, line.account),
        [h.debit]: isDebit ? money(line.amount.amount) : 0,
        [h.credit]: isDebit ? 0 : money(line.amount.amount),
        [h.margin]: money(s.margin),
        [h.vatOutput]: money(s.outputVat),
        [h.vatInput]: money(s.inputVat),
        [h.netVat]: money(s.netVatToZatca),
        ...collectionCells(c, lang),
        [h.currency]: line.amount.currency,
      });
    }
  }
  return rows;
}

function csvCell(value: string | number): string {
  if (typeof value === "number") return value.toFixed(2);
  const needsQuote = /[",\r\n]/.test(value);
  const escaped = value.replace(/"/g, '""');
  return needsQuote ? `"${escaped}"` : escaped;
}

/** Serialize rows to CSV text (numbers as money 2dp). */
export function rowsToCsv(rows: ExportRow[]): string {
  if (rows.length === 0) return "";
  const headers = Object.keys(rows[0]);
  const lines = [headers.map(csvCell).join(",")];
  for (const row of rows) {
    lines.push(headers.map((key) => csvCell(row[key] ?? "")).join(","));
  }
  return lines.join("\r\n");
}

/** Full flat CSV string for the current settlement scope. */
export function buildSettlementCsv(input: SettlementExportInput): string {
  return rowsToCsv(buildFlatRows(input));
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

/** Download a flat CSV (UTF-8 BOM so Excel renders Arabic correctly). */
export function downloadSettlementCsv(input: SettlementExportInput): void {
  const csv = buildSettlementCsv(input);
  const blob = new Blob([`\uFEFF${csv}`], { type: "text/csv;charset=utf-8;" });
  triggerDownload(blob, `settlement-${timestamp()}.csv`);
}

/** Download a two-sheet .xlsx (Cases summary + Journal lines) via SheetJS. */
export async function downloadSettlementXlsx(input: SettlementExportInput): Promise<void> {
  const XLSX = await import("xlsx");
  const { lang } = input;
  const caseRows = buildCaseRows(input);
  const lineRows = buildJournalLineRows(input);

  const wb = XLSX.utils.book_new();
  const wsCases = XLSX.utils.json_to_sheet(caseRows);
  const wsLines = XLSX.utils.json_to_sheet(lineRows);
  applyMoneyFormat(XLSX, wsCases);
  applyMoneyFormat(XLSX, wsLines);

  XLSX.utils.book_append_sheet(wb, wsCases, t(lang, "exportSheetCases").slice(0, 31));
  XLSX.utils.book_append_sheet(wb, wsLines, t(lang, "exportSheetLines").slice(0, 31));
  XLSX.writeFile(wb, `settlement-${timestamp()}.xlsx`);
}

/** Apply a 2-decimal money format to every numeric cell in a worksheet. */
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
