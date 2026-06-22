/**
 * Direction 2 — partner payable VAT display split (MOCK / display only).
 *
 * The partner issues a VAT-inclusive invoice to Zahy; every payable figure shown on the
 * partner statement or its proforma must split into ex-VAT + input VAT via the single
 * {@link splitInclusiveVat} helper — never show VAT 0.00 on a VAT-inclusive amount.
 */
import { splitInclusiveVat, VAT_RATE } from "@/lib/reports/settlementReports";

const r2 = (n: number) => Math.round(n * 100) / 100;

export interface PartnerPayableVatSplit {
  exVat: number;
  inputVat: number;
  inclusive: number;
}

/** Split one VAT-inclusive partner payable for UI / proforma lines. */
export function splitPartnerPayableInclusive(inclusive: number): PartnerPayableVatSplit {
  if (!inclusive) return { exVat: 0, inputVat: 0, inclusive: 0 };
  const { exVat, vat } = splitInclusiveVat(inclusive, VAT_RATE);
  return { exVat, inputVat: vat, inclusive: r2(inclusive) };
}

/** Roll up line splits — sums ex-VAT and input VAT; grand total stays the read-model payable. */
export function rollupPartnerPayableVat(
  lines: PartnerPayableVatSplit[],
  grandTotalInclusive: number,
): PartnerPayableVatSplit {
  return {
    exVat: r2(lines.reduce((s, l) => s + l.exVat, 0)),
    inputVat: r2(lines.reduce((s, l) => s + l.inputVat, 0)),
    inclusive: r2(grandTotalInclusive),
  };
}
