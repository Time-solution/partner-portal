/**
 * Manual (ad-hoc) invoice math + validation (PURE — used by the form, the data
 * source, the PDF preview, and tests).
 *
 * The VAT split uses the SINGLE locked inclusive back-out shared by the rest of
 * Finance ({@link splitInclusiveVat}, round-per-line, 2dp away-from-zero). This is
 * the SAME formula the backend (FinanceVat / VatMath) applies, so the preview grand
 * total is identical to what the backend persists. Do NOT re-derive VAT here.
 */
import { VAT_RATE, splitInclusiveVat } from "@/lib/reports/settlementReports";
import type { ManualInvoiceLine, ManualInvoiceLineInput } from "@/lib/data/types";

/** Default GL account for manual fee lines (mirrors backend 4200 Fee Revenue). */
export const MANUAL_LINE_ACCOUNT_CODE = "4200";

const r2 = (n: number) => Math.round(n * 100) / 100;

export interface ManualInvoiceTotals {
  subtotalNet: number;
  vatTotal: number;
  grandTotalInclusive: number;
}

export type ManualLineField = "description" | "quantity" | "unitPriceInclusive" | "lines";

/** One validation problem keyed by line index (index -1 = the whole form). */
export interface ManualLineError {
  index: number;
  field: ManualLineField;
}

/**
 * Per-line VAT split — IDENTICAL inclusive back-out to the backend and to the rest
 * of Finance: lineTotal = round(qty × unitInclusive); net = round(lineTotal / 1.15);
 * vat = lineTotal − net. Round-per-line.
 */
export function computeManualLine(
  input: ManualInvoiceLineInput,
  lineNo: number,
  rate: number = VAT_RATE,
): ManualInvoiceLine {
  const lineTotalInclusive = r2(input.quantity * input.unitPriceInclusive);
  const { exVat, vat } = splitInclusiveVat(lineTotalInclusive, rate);
  return {
    lineNo,
    description: input.description.trim(),
    quantity: input.quantity,
    unitPriceInclusive: r2(input.unitPriceInclusive),
    lineTotalInclusive,
    vatNet: exVat,
    vatAmount: vat,
    accountCode: input.accountCode?.trim() || MANUAL_LINE_ACCOUNT_CODE,
  };
}

export function computeManualInvoiceLines(
  inputs: ManualInvoiceLineInput[],
  rate: number = VAT_RATE,
): ManualInvoiceLine[] {
  return inputs.map((input, i) => computeManualLine(input, i + 1, rate));
}

/** Sum the per-line splits — net/vat/grand are summed from the rounded line figures. */
export function computeManualInvoiceTotals(lines: ManualInvoiceLine[]): ManualInvoiceTotals {
  return {
    subtotalNet: r2(lines.reduce((s, l) => s + l.vatNet, 0)),
    vatTotal: r2(lines.reduce((s, l) => s + l.vatAmount, 0)),
    grandTotalInclusive: r2(lines.reduce((s, l) => s + l.lineTotalInclusive, 0)),
  };
}

/** Preview lines + totals straight from the raw form inputs. */
export function previewManualInvoice(
  inputs: ManualInvoiceLineInput[],
  rate: number = VAT_RATE,
): { lines: ManualInvoiceLine[]; totals: ManualInvoiceTotals } {
  const lines = computeManualInvoiceLines(inputs, rate);
  return { lines, totals: computeManualInvoiceTotals(lines) };
}

/**
 * Form-level validation — mirrors the backend domain guards:
 *   • at least one line       (backend Zahy.Finance:081 ManualInvoiceInvalidLines)
 *   • description required
 *   • quantity > 0
 *   • unit price > 0          (backend rejects ≤ 0)
 */
export function validateManualInvoiceLines(inputs: ManualInvoiceLineInput[]): ManualLineError[] {
  if (inputs.length === 0) return [{ index: -1, field: "lines" }];
  const errors: ManualLineError[] = [];
  inputs.forEach((line, index) => {
    if (!line.description.trim()) errors.push({ index, field: "description" });
    if (!(line.quantity > 0)) errors.push({ index, field: "quantity" });
    if (!(line.unitPriceInclusive > 0)) errors.push({ index, field: "unitPriceInclusive" });
  });
  return errors;
}

export function isManualInvoiceValid(inputs: ManualInvoiceLineInput[]): boolean {
  return validateManualInvoiceLines(inputs).length === 0;
}
