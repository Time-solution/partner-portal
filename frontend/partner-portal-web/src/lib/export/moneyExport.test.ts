import { afterAll, beforeAll, describe, expect, it, vi } from "vitest";
import { buildInvoiceRows, buildMoneyCsv, buildReceiptRows } from "./moneyExport";
import type { Receipt, SubscriptionBillingPeriod } from "@/lib/data/types";

// Payment status derives from the REAL clock vs the fixture's dueDate (2026-07-15) — pin "now"
// before the due date so "Partial" never flips to "Overdue" when the calendar catches up
// (this exact rollover broke the suite on 2026-07-15).
beforeAll(() => {
  vi.useFakeTimers({ now: new Date("2026-07-01T00:00:00Z"), toFake: ["Date"] });
});
afterAll(() => {
  vi.useRealTimers();
});

const sar = (amount: number) => ({ amount, currency: "SAR", vatInclusive: true });

const invoices: SubscriptionBillingPeriod[] = [
  {
    id: "bill-1",
    partnerId: "p-1",
    tenantId: "t-1",
    merchantName: "Quick Bites Co.",
    periodKey: "2026-06",
    feeInclusive: sar(115),
    outputVat: 15,
    netFee: 100,
    billingChargeId: "chg-1",
    invoiceNumber: "INV-2026-06-0042",
    journalBalanced: true,
    status: "Invoiced",
    dueDate: "2026-07-15T00:00:00Z",
    payments: [{ id: "pay-1", amount: sar(40), date: "2026-06-18", method: "Transfer", reference: "x" }],
  },
];

const receipts: Receipt[] = [
  {
    id: "rcpt-1",
    receiptNo: "ZR-2026-0001",
    invoiceRef: "INV-2026-06-0042",
    billingPeriodId: "bill-1",
    partnerId: "p-1",
    tenantId: "t-1",
    merchantName: "Quick Bites Co.",
    amount: sar(40),
    date: "2026-06-18",
    method: "Transfer",
    sent: true,
  },
];

describe("buildInvoiceRows", () => {
  it("includes payment status, paid and outstanding (EN headers)", () => {
    const [row] = buildInvoiceRows({ invoices, receipts, lang: "en" });
    expect(row["Invoiced"]).toBe(115);
    expect(row["Paid"]).toBe(40);
    expect(row["Outstanding"]).toBe(75);
    expect(row["Payment"]).toBe("Partial");
  });

  it("localizes headers + status to Arabic", () => {
    const [row] = buildInvoiceRows({ invoices, receipts, lang: "ar" });
    expect(row["حالة السداد"]).toBe("سداد جزئي");
  });
});

describe("buildReceiptRows", () => {
  it("emits a row per receipt with clean invoice ref", () => {
    const rows = buildReceiptRows({ invoices, receipts, lang: "en" });
    expect(rows).toHaveLength(1);
    expect(rows[0]["Receipt no."]).toBe("ZR-2026-0001");
    expect(rows[0]["Paid"]).toBe(40);
  });
});

describe("buildMoneyCsv", () => {
  it("contains both invoice and receipt blocks", () => {
    const csv = buildMoneyCsv({ invoices, receipts, lang: "en" });
    expect(csv).toContain("INV-2026-06-0042");
    expect(csv).toContain("ZR-2026-0001");
    // Blank-line separator between the two blocks.
    expect(csv).toContain("\r\n\r\n");
  });
});
