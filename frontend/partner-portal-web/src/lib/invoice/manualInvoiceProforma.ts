/**
 * Map a {@link ManualInvoice} into a {@link ProformaDocument} for the MOCK PDF preview.
 *
 * PURE pass-through: every figure (net / VAT / inclusive / grand total) is taken verbatim
 * from the already-computed invoice — money is never recomputed here. The proforma carries
 * the BETA mark (NOT a ZATCA tax invoice), matching the rest of the cycle.
 */
import type { ManualInvoice } from "@/lib/data/types";
import type { Lang } from "@/lib/i18n";
import { DEFAULT_PLATFORM_PROFILE, emptyOrgProfile } from "@/lib/profile/orgProfile";
import {
  L,
  buildProforma,
  type ProformaDocument,
  type ProformaLine,
} from "./proformaDocument";

export function buildManualInvoiceProforma(invoice: ManualInvoice, lang: Lang): ProformaDocument {
  const lines: ProformaLine[] = invoice.lines.map((line) => ({
    serviceType: line.description,
    billingType: L(lang, "بند يدوي", "Manual line"),
    basis: `${line.quantity} × ${line.unitPriceInclusive.toFixed(2)}`,
    exVat: line.vatNet,
    vat: line.vatAmount,
    inclusive: line.lineTotalInclusive,
  }));

  return buildProforma({
    kind: "invoice",
    docNumber: invoice.invoiceNumber,
    periodLabel: invoice.issueDate.slice(0, 10),
    issueDate: invoice.issueDate.slice(0, 10),
    currency: invoice.currency,
    issuer: DEFAULT_PLATFORM_PROFILE,
    billTo: emptyOrgProfile({ name: invoice.recipient }),
    billToFallbackName: invoice.recipient,
    serviceContext: L(lang, "فاتورة يدوية", "Manual invoice"),
    lines,
    totals: {
      subtotalExVat: invoice.subtotalNet,
      totalVat: invoice.vatTotal,
      grandTotalInclusive: invoice.grandTotalInclusive,
      paidToDate: 0,
      remaining: invoice.grandTotalInclusive,
    },
    lang,
  });
}
