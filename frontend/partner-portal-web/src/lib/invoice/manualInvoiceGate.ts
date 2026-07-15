import type { ManualInvoice, Partner } from "@/lib/data/types";
import { computeManualInvoiceLines, computeManualInvoiceTotals } from "./manualInvoice";

/**
 * P5 — FE mirror of the backend pre-invoice validation gate (FinanceInvoicePreIssueGate). Same
 * checks, same architect-assigned codes, same multi-violation aggregation (no fail-fast). The VAT
 * recompute reuses the SHARED FE VAT helpers (computeManualInvoiceLines — no new money logic).
 * The BE gate verdict is the source of truth; this mirror keeps the mock faithful.
 */

export interface ManualInvoiceGateViolation {
  /** Backend code suffix, e.g. "081" → i18n key manualInvGate_081. */
  code: string;
  i18nKey: string;
}

export class ManualInvoiceGateError extends Error {
  readonly violations: ManualInvoiceGateViolation[];

  constructor(violations: ManualInvoiceGateViolation[]) {
    super(`Pre-invoice validation failed (${violations.map((v) => v.code).join(", ")}).`);
    this.name = "ManualInvoiceGateError";
    this.violations = violations;
  }
}

export interface ManualInvoiceGateContext {
  partners: Partner[];
  /** All manual invoices (for the duplicate-reference check). */
  invoices: ManualInvoice[];
  now: Date;
  /** Mirrors IFinancePeriodStatusProvider — the null-object default answers open. */
  periodOpen?: boolean;
}

const violation = (code: string): ManualInvoiceGateViolation => ({
  code,
  i18nKey: `manualInvGate_${code}`,
});

const centsEqual = (a: number, b: number) => Math.round(a * 100) === Math.round(b * 100);

export function evaluateManualInvoiceGate(
  invoice: ManualInvoice,
  ctx: ManualInvoiceGateContext,
): ManualInvoiceGateViolation[] {
  const violations: ManualInvoiceGateViolation[] = [];

  // [:081 absorbed] at least one line + non-positive qty/price.
  if (invoice.lines.length === 0) {
    violations.push(violation("081"));
  } else if (invoice.lines.some((l) => l.quantity <= 0 || l.unitPriceInclusive <= 0)) {
    violations.push(violation("081"));
  }

  if (invoice.lines.length > 0) {
    // Recompute via the SHARED helpers (integer-cents compare — the JS float rule).
    const recomputed = computeManualInvoiceLines(
      invoice.lines.map((l) => ({
        description: l.description,
        quantity: l.quantity,
        unitPriceInclusive: l.unitPriceInclusive,
      })),
    );
    const totals = computeManualInvoiceTotals(recomputed);

    // [:080 absorbed] header total vs recomputed sum — never trusted.
    if (!centsEqual(totals.grandTotalInclusive, invoice.grandTotalInclusive)) {
      violations.push(violation("080"));
    }

    // [:083] per-line VAT split vs the shared-source recompute.
    const vatMismatch = invoice.lines.some((stored, i) => {
      const expected = recomputed[i];
      return (
        !centsEqual(stored.lineTotalInclusive, expected.lineTotalInclusive) ||
        !centsEqual(stored.vatNet, expected.vatNet) ||
        !centsEqual(stored.vatAmount, expected.vatAmount)
      );
    });
    if (vatMismatch) {
      violations.push(violation("083"));
    }
  }

  // [:084 / :088] counterparty — partner recipients checked; merchant/External skip per the BE rule.
  if (invoice.recipientType === "Partner") {
    const partner = ctx.partners.find((p) => p.id === invoice.recipientReference);
    if (!partner) {
      violations.push(violation("084"));
    } else {
      if (partner.status !== "Active") {
        violations.push(violation("084"));
      }
      if (partner.participationMode === "ReflectionOnly") {
        violations.push(violation("088"));
      }
    }
  }

  // [:085] duplicate reference — another invoice already carries this number.
  if (ctx.invoices.some((m) => m.id !== invoice.id && m.invoiceNumber === invoice.invoiceNumber)) {
    violations.push(violation("085"));
  }

  // [:086] date invalid — future issue date, or the period is not open (default open).
  if (new Date(invoice.issueDate).getTime() > ctx.now.getTime()) {
    violations.push(violation("086"));
  }
  if (ctx.periodOpen === false) {
    violations.push(violation("086"));
  }

  // [:087] is LedgerDerived-only — the mock only issues Manual invoices, so it never applies here.

  return violations;
}

export function ensureManualInvoiceGate(invoice: ManualInvoice, ctx: ManualInvoiceGateContext): void {
  const violations = evaluateManualInvoiceGate(invoice, ctx);
  if (violations.length > 0) {
    throw new ManualInvoiceGateError(violations);
  }
}
