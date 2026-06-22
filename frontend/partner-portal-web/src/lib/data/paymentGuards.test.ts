import { describe, expect, it } from "vitest";
import {
  assertPaymentWithinOutstanding,
  findDuplicatePayment,
  type InvoicePayment,
  type SubscriptionBillingPeriod,
} from "./types";

const sar = (amount: number) => ({ amount, currency: "SAR", vatInclusive: true });

function payment(amount: number, reference: string): InvoicePayment {
  return { id: `pay-${reference}`, amount: sar(amount), date: "2026-06-15T10:00:00Z", method: "Transfer", reference };
}

function invoice(over: Partial<SubscriptionBillingPeriod> = {}): SubscriptionBillingPeriod {
  return {
    id: "bill-1",
    partnerId: "p-1",
    tenantId: "t-1",
    merchantName: "Quick Bites Co.",
    periodKey: "2026-06",
    feeInclusive: sar(100),
    outputVat: 13.04,
    netFee: 86.96,
    billingChargeId: "chg-1",
    journalBalanced: true,
    status: "Invoiced",
    ...over,
  };
}

// FE mirror of the backend Payment overpayment guard.
describe("assertPaymentWithinOutstanding", () => {
  it("rejects an overpayment (120 on a 100 balance)", () => {
    expect(() => assertPaymentWithinOutstanding(invoice(), 120)).toThrowError(/Overpayment/);
  });

  it("rejects an overpayment across multiple receipts (70 paid, +40 on remaining 30)", () => {
    expect(() =>
      assertPaymentWithinOutstanding(invoice({ payments: [payment(70, "RCPT-1")] }), 40),
    ).toThrowError(/Overpayment/);
  });

  it("accepts paying exactly the outstanding balance (partial + partial = paid)", () => {
    expect(() => assertPaymentWithinOutstanding(invoice(), 60)).not.toThrow();
    expect(() =>
      assertPaymentWithinOutstanding(invoice({ payments: [payment(60, "RCPT-1")] }), 40),
    ).not.toThrow();
  });
});

// FE mirror of the backend idempotency key.
describe("findDuplicatePayment", () => {
  it("detects a re-submit with the same reference (double-submit is a no-op upstream)", () => {
    const period = invoice({ payments: [payment(60, "TRF-1")] });
    expect(findDuplicatePayment(period, "TRF-1")?.id).toBe("pay-TRF-1");
    expect(findDuplicatePayment(period, " trf-1 ")?.id).toBe("pay-TRF-1"); // trimmed + case-insensitive
  });

  it("treats an empty/auto reference as unique (never dedupes)", () => {
    const period = invoice({ payments: [payment(60, "TRF-1")] });
    expect(findDuplicatePayment(period, "")).toBeUndefined();
    expect(findDuplicatePayment(period, "TRF-2")).toBeUndefined();
  });
});
