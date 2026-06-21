import { describe, expect, it } from "vitest";
import {
  deriveBalances,
  deriveInvoicePayment,
  sumPayments,
  type InvoicePayment,
  type SubscriptionBillingPeriod,
} from "./types";

const sar = (amount: number) => ({ amount, currency: "SAR", vatInclusive: true });

function payment(amount: number, date = "2026-06-15T10:00:00Z"): InvoicePayment {
  return { id: `pay-${amount}`, amount: sar(amount), date, method: "Transfer", reference: "ref" };
}

function invoice(over: Partial<SubscriptionBillingPeriod> = {}): SubscriptionBillingPeriod {
  return {
    id: "bill-1",
    partnerId: "p-1",
    tenantId: "t-1",
    merchantName: "Quick Bites Co.",
    periodKey: "2026-06",
    feeInclusive: sar(115),
    outputVat: 15,
    netFee: 100,
    billingChargeId: "chg-1",
    journalBalanced: true,
    status: "Invoiced",
    ...over,
  };
}

const NOW = new Date("2026-06-20T00:00:00Z");

describe("sumPayments", () => {
  it("sums payment amounts (2dp) and tolerates undefined", () => {
    expect(sumPayments(undefined)).toBe(0);
    expect(sumPayments([payment(40), payment(35)])).toBe(75);
  });
});

describe("deriveInvoicePayment", () => {
  it("Pending when nothing paid and not past due", () => {
    const s = deriveInvoicePayment(invoice({ dueDate: "2026-07-01T00:00:00Z" }), NOW);
    expect(s).toMatchObject({ total: 115, amountPaid: 0, amountOutstanding: 115, status: "Pending" });
  });

  it("Partial when some (but not all) is paid before due", () => {
    const s = deriveInvoicePayment(
      invoice({ dueDate: "2026-07-01T00:00:00Z", payments: [payment(40)] }),
      NOW,
    );
    expect(s).toMatchObject({ amountPaid: 40, amountOutstanding: 75, status: "Partial" });
  });

  it("Paid when fully covered (multiple partials)", () => {
    const s = deriveInvoicePayment(invoice({ payments: [payment(100), payment(15)] }), NOW);
    expect(s).toMatchObject({ amountPaid: 115, amountOutstanding: 0, status: "Paid" });
  });

  it("Overdue when unpaid and past due date", () => {
    const s = deriveInvoicePayment(invoice({ dueDate: "2026-06-10T00:00:00Z" }), NOW);
    expect(s.status).toBe("Overdue");
  });

  it("Overdue when partially paid and past due date", () => {
    const s = deriveInvoicePayment(
      invoice({ dueDate: "2026-06-10T00:00:00Z", payments: [payment(50)] }),
      NOW,
    );
    expect(s.status).toBe("Overdue");
  });
});

describe("deriveBalances", () => {
  it("aggregates invoices per merchant with outstanding + overdue counts", () => {
    const rows = deriveBalances(
      [
        invoice({ id: "b1", tenantId: "t-1", payments: [payment(115)] }), // paid
        invoice({ id: "b2", tenantId: "t-1", dueDate: "2026-06-10T00:00:00Z" }), // overdue 115
        invoice({ id: "b3", tenantId: "t-2", merchantName: "Al Noor", payments: [payment(40)] }),
      ],
      NOW,
    );
    const t1 = rows.find((r) => r.key === "t-1")!;
    expect(t1.invoiced).toBe(230);
    expect(t1.paid).toBe(115);
    expect(t1.outstanding).toBe(115);
    expect(t1.overdueCount).toBe(1);
    expect(t1.invoiceCount).toBe(2);
    // Sorted by outstanding desc → t-1 (115) before t-2 (75).
    expect(rows[0].key).toBe("t-1");
  });
});
