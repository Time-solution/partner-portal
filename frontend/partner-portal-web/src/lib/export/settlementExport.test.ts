import { describe, expect, it } from "vitest";
import type { SettlementCase, SettlementReversal } from "@/lib/data/types";
import {
  buildCaseRows,
  buildFlatRows,
  buildJournalLineRows,
  buildSettlementCsv,
  buildSettlementWorkbook,
  exportHeaders,
  rowsToCsv,
} from "./settlementExport";

const sar = (amount: number) => ({ amount, currency: "SAR", vatInclusive: false });

const caseA: SettlementCase = {
  id: "stl-case-7002",
  partnerId: "p-salasa",
  partnerName: "Salasa Delivery",
  tenantId: "t-001",
  externalTransactionId: "order:ord-7002:v1",
  book: "Marketplace",
  state: "Invoiced",
  createdAt: "2026-06-10T11:00:00Z",
  journal: {
    currency: "SAR",
    totalDebits: sar(14.3),
    totalCredits: sar(14.3),
    lines: [
      { account: "AggregatorClearing", direction: "Debit", amount: sar(13) },
      { account: "VatInput", direction: "Debit", amount: sar(1.3) },
      { account: "DeliveryCost", direction: "Credit", amount: sar(10) },
      { account: "ShippingMarginRevenue", direction: "Credit", amount: sar(2.6) },
      { account: "VatOutput", direction: "Credit", amount: sar(1.7) },
    ],
  },
};

const caseCod: SettlementCase = {
  id: "stl-case-9001",
  partnerId: "p-salasa",
  partnerName: "Salasa Delivery",
  tenantId: "t-001",
  externalTransactionId: "order:ord-9001:v1",
  book: "Marketplace",
  state: "Allocated",
  createdAt: "2026-06-19T16:40:00Z",
  journal: { currency: "SAR", totalDebits: sar(230), totalCredits: sar(230), lines: [] },
  collection: {
    paymentMethod: "COD",
    collectedBy: "DeliveryCompany",
    totalCollected: sar(113),
    deductions: [{ label: "Delivery cost retained", amount: sar(10), kind: "Flat" }],
    netRemitted: sar(103),
    // Four-way: 113 = 100 + 10 + 2.60 + 0.40
    split: { merchant: sar(100), deliveryCompany: sar(10), zahy: sar(2.6), zatca: sar(0.4) },
  },
};

const input = {
  cases: [caseA],
  merchantName: (tenantId?: string) => (tenantId === "t-001" ? "Al Noor Restaurant" : "—"),
  lang: "en" as const,
};

describe("buildCaseRows", () => {
  it("produces one summary row per case with clean order id and money fields", () => {
    const rows = buildCaseRows(input);
    const h = exportHeaders("en");
    expect(rows).toHaveLength(1);
    expect(rows[0][h.order]).toBe("Order #7002 · v1");
    expect(rows[0][h.merchant]).toBe("Al Noor Restaurant");
    expect(rows[0][h.buy]).toBe(10);
    expect(rows[0][h.sell]).toBe(13);
    expect(rows[0][h.margin]).toBe(2.6);
    expect(rows[0][h.vatOutput]).toBe(1.7);
    expect(rows[0][h.vatInput]).toBe(1.3);
    expect(rows[0][h.netVat]).toBe(0.4);
  });
});

describe("collection columns", () => {
  it("includes payment method, total collected, deductions, net remitted and the 4-way split", () => {
    const rows = buildCaseRows({ ...input, cases: [caseCod] });
    const h = exportHeaders("en");
    expect(rows[0][h.paymentMethod]).toBe("Cash on delivery");
    expect(rows[0][h.totalCollected]).toBe(113);
    expect(rows[0][h.deductions]).toBe(10);
    expect(rows[0][h.netRemitted]).toBe(103);
    expect(rows[0][h.splitMerchant]).toBe(100);
    expect(rows[0][h.splitDelivery]).toBe(10);
    expect(rows[0][h.splitZahy]).toBe(2.6);
    expect(rows[0][h.splitZatca]).toBe(0.4);
    // The collected total goes four ways (3 parties + tax).
    const sum =
      Number(rows[0][h.splitMerchant]) +
      Number(rows[0][h.splitDelivery]) +
      Number(rows[0][h.splitZahy]) +
      Number(rows[0][h.splitZatca]);
    expect(Math.round(sum * 100) / 100).toBe(113);
  });

  it("renders blank/zero collection cells for legacy cases", () => {
    const rows = buildCaseRows(input);
    const h = exportHeaders("en");
    expect(rows[0][h.paymentMethod]).toBe("—");
    expect(rows[0][h.totalCollected]).toBe(0);
  });
});

describe("buildJournalLineRows", () => {
  it("emits one row per journal line with split debit/credit columns", () => {
    const rows = buildJournalLineRows(input);
    const h = exportHeaders("en");
    expect(rows).toHaveLength(5);
    const totalDebit = rows.reduce((s, r) => s + Number(r[h.debit]), 0);
    const totalCredit = rows.reduce((s, r) => s + Number(r[h.credit]), 0);
    expect(totalDebit).toBeCloseTo(14.3, 2);
    expect(totalCredit).toBeCloseTo(14.3, 2);
    expect(totalDebit).toBeCloseTo(totalCredit, 2);
  });
});

describe("rowsToCsv", () => {
  it("renders a header row + flat journal-line rows with money 2dp", () => {
    const csv = rowsToCsv(buildFlatRows(input));
    const lines = csv.split("\r\n");
    expect(lines).toHaveLength(6); // header + 5 journal lines
    expect(lines[0]).toContain("Order");
    expect(lines[0]).toContain("Debit");
    expect(csv).toContain("13.00");
  });

  it("returns empty string for no rows", () => {
    expect(rowsToCsv([])).toBe("");
  });
});

const reversalForCaseA: SettlementReversal = {
  id: "stl-rev-7002",
  originalCaseId: "stl-case-7002",
  partnerId: "p-salasa",
  partnerName: "Salasa Delivery",
  tenantId: "t-001",
  orderLineId: "",
  journal: {
    currency: "SAR",
    totalDebits: sar(14.3),
    totalCredits: sar(14.3),
    lines: [
      { account: "DeliveryCost", direction: "Debit", amount: sar(10) },
      { account: "AggregatorClearing", direction: "Credit", amount: sar(13) },
      { account: "ShippingMarginRevenue", direction: "Debit", amount: sar(2.6) },
      { account: "VatOutput", direction: "Debit", amount: sar(1.7) },
      { account: "VatInput", direction: "Credit", amount: sar(1.3) },
    ],
  },
  netsToZero: true,
  reason: "Duplicate disbursement reversed",
  reversedBy: "accountant@zahy.dev",
  createdAt: "2026-06-20T11:00:00Z",
};

describe("settlementExport reversals", () => {
  it("settlementExport_Xlsx_Has_Reversals_Sheet_When_Reversals_Present", async () => {
    const wb = await buildSettlementWorkbook(input, [reversalForCaseA]);
    expect(wb.SheetNames).toContain("Reversals");
    const ws = wb.Sheets["Reversals"];
    const json = (await import("xlsx")).utils.sheet_to_json<Record<string, unknown>>(ws);
    expect(json).toHaveLength(1);
    const h = exportHeaders("en");
    expect(json[0][h.reversalId]).toBe("stl-rev-7002");
    expect(json[0][h.originalCaseRef]).toBe("Order #7002 · v1");
    expect(json[0][h.reversedBy]).toBe("accountant@zahy.dev");
    expect(json[0][h.netsToZero]).toBe("Y");
  });

  it("settlementExport_Xlsx_Omits_Reversals_Sheet_When_None", async () => {
    const wb = await buildSettlementWorkbook(input, []);
    expect(wb.SheetNames).not.toContain("Reversals");
  });

  it("settlementExport_Csv_Appends_Reversals_Section", () => {
    const csv = buildSettlementCsv(input, [reversalForCaseA]);
    const plainCsv = buildSettlementCsv(input);
    // Main section unchanged; reversals appended after a blank line + REVERSALS header.
    expect(csv.startsWith(plainCsv)).toBe(true);
    expect(csv).toContain("REVERSALS");
    expect(csv).toContain("stl-rev-7002");
    expect(csv).toContain("accountant@zahy.dev");
    const blankLineIdx = csv.indexOf("\r\n\r\nREVERSALS");
    expect(blankLineIdx).toBeGreaterThan(-1);
  });

  it("adds a ReversedBy column to the main case sheet when a reversal points to the case", () => {
    const h = exportHeaders("en");
    const withRev = buildCaseRows(input, [reversalForCaseA]);
    expect(withRev[0][h.reversedBy]).toBe("accountant@zahy.dev");
    const withoutRev = buildCaseRows(input);
    expect(withoutRev[0][h.reversedBy]).toBe("");
  });
});
