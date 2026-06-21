import { describe, expect, it } from "vitest";
import {
  buildCollectionJournal,
  collectionCashFlow,
  deriveSettlementSummary,
  sumDeductions,
  type CollectionRemittance,
  type SettlementJournalLine,
} from "./types";

const sar = (amount: number) => ({ amount, currency: "SAR", vatInclusive: true });

// Confirmed model: customer pays 113 = items 100 + delivery 13 (buy 10 / sell 13).
const cod: CollectionRemittance = {
  paymentMethod: "COD",
  collectedBy: "DeliveryCompany",
  totalCollected: sar(113),
  deductions: [{ label: "Delivery cost retained", amount: sar(10), kind: "Flat" }],
  netRemitted: sar(103),
  split: { merchant: sar(100), deliveryCompany: sar(10), zahy: sar(2.6), zatca: sar(0.4) },
};

const online: CollectionRemittance = {
  paymentMethod: "Online",
  collectedBy: "Gateway",
  totalCollected: sar(113),
  deductions: [],
  netRemitted: sar(110),
  split: { merchant: sar(100), deliveryCompany: sar(10), zahy: sar(2.6), zatca: sar(0.4) },
};

const r2 = (n: number) => Math.round(n * 100) / 100;

/** Sum DR and CR for the lines of a single grouped entry. */
function entryTotals(lines: SettlementJournalLine[], entry: string) {
  const grp = lines.filter((l) => l.entry === entry);
  const dr = r2(grp.filter((l) => l.direction === "Debit").reduce((s, l) => s + l.amount.amount, 0));
  const cr = r2(grp.filter((l) => l.direction === "Credit").reduce((s, l) => s + l.amount.amount, 0));
  return { dr, cr };
}

function overallTotals(lines: SettlementJournalLine[]) {
  const dr = r2(lines.filter((l) => l.direction === "Debit").reduce((s, l) => s + l.amount.amount, 0));
  const cr = r2(lines.filter((l) => l.direction === "Credit").reduce((s, l) => s + l.amount.amount, 0));
  return { dr, cr };
}

describe("sumDeductions / cash flow", () => {
  it("sums deductions", () => expect(sumDeductions(cod.deductions)).toBe(10));
  it("COD is inbound, Online is outbound", () => {
    expect(collectionCashFlow(cod)).toBe("Inbound");
    expect(collectionCashFlow(online)).toBe("Outbound");
  });
});

describe("buildCollectionJournal — COD", () => {
  const j = buildCollectionJournal(cod);

  it("headline total = the real money collected (113), not 230", () => {
    expect(j.totalDebits.amount).toBe(113);
    expect(j.totalCredits.amount).toBe(113);
  });

  it("recognition entry balances at 113 (DR = CR)", () => {
    const { dr, cr } = entryTotals(j.lines, "Collection");
    expect(dr).toBe(113);
    expect(cr).toBe(113);
  });

  it("every grouped entry is individually balanced", () => {
    for (const entry of ["Collection", "DeliveryCost", "Remittance"]) {
      const { dr, cr } = entryTotals(j.lines, entry);
      expect(dr).toBe(cr);
    }
    const overall = overallTotals(j.lines);
    expect(overall.dr).toBe(overall.cr);
  });

  it("books items 100 as Merchant Payable only (no items VAT)", () => {
    const merchant = j.lines.find(
      (l) => l.account === "MerchantPayable" && l.direction === "Credit" && l.entry === "Collection",
    );
    expect(merchant?.amount.amount).toBe(100);
  });

  it("carries the delivery-leg VAT: output 1.70, input 1.30, net 0.40", () => {
    const out = j.lines.find((l) => l.account === "VatOutput" && l.entry === "Collection");
    const inp = j.lines.find((l) => l.account === "VatInput" && l.entry === "DeliveryCost");
    expect(out?.amount.amount).toBe(1.7);
    expect(inp?.amount.amount).toBe(1.3);

    const s = deriveSettlementSummary(j);
    expect(s.outputVat).toBe(1.7);
    expect(s.inputVat).toBe(1.3);
    expect(s.netVatToZatca).toBe(0.4);
  });

  it("derives the delivery leg sell 13 / buy 10 / margin 2.60", () => {
    const s = deriveSettlementSummary(j);
    expect(s.sellPrice.amount).toBe(13);
    expect(s.buyPrice.amount).toBe(10);
    expect(s.margin).toBe(2.6);
  });

  it("never opens-and-closes a clearing account within one entry", () => {
    for (const entry of ["Collection", "DeliveryCost", "Remittance"]) {
      const grp = j.lines.filter((l) => l.entry === entry);
      for (const acct of new Set(grp.map((l) => l.account))) {
        const hasDr = grp.some((l) => l.account === acct && l.direction === "Debit");
        const hasCr = grp.some((l) => l.account === acct && l.direction === "Credit");
        expect(hasDr && hasCr).toBe(false);
      }
    }
  });

  it("remittance is a SEPARATE cash entry: delivery co remits net 103", () => {
    const cash = j.lines.find(
      (l) => l.account === "Cash" && l.direction === "Debit" && l.entry === "Remittance",
    );
    expect(cash?.amount.amount).toBe(103);
  });
});

describe("buildCollectionJournal — Online", () => {
  const j = buildCollectionJournal(online);

  it("headline total = 113, balanced", () => {
    expect(j.totalDebits.amount).toBe(113);
    const overall = overallTotals(j.lines);
    expect(overall.dr).toBe(overall.cr);
  });

  it("gateway collects 113 in the recognition entry", () => {
    const cashIn = j.lines.find(
      (l) => l.account === "Cash" && l.direction === "Debit" && l.entry === "Collection",
    );
    expect(cashIn?.amount.amount).toBe(113);
  });

  it("pays merchant + delivery out as a separate cash entry (110)", () => {
    const cashOut = j.lines.find(
      (l) => l.account === "Cash" && l.direction === "Credit" && l.entry === "Remittance",
    );
    expect(cashOut?.amount.amount).toBe(110);
  });

  it("still carries the delivery VAT and split", () => {
    const s = deriveSettlementSummary(j);
    expect(s.sellPrice.amount).toBe(13);
    expect(s.margin).toBe(2.6);
    expect(s.outputVat).toBe(1.7);
    expect(s.inputVat).toBe(1.3);
    expect(s.netVatToZatca).toBe(0.4);
  });
});
