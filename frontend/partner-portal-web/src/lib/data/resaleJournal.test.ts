import { describe, expect, it } from "vitest";
import {
  buildResaleJournal,
  deriveSettlementSummary,
  invertJournal,
  type SettlementJournalLine,
} from "./types";

const sumEntry = (lines: SettlementJournalLine[], entry: string, dir: "Debit" | "Credit") =>
  Math.round(
    lines
      .filter((l) => l.entry === entry && l.direction === dir)
      .reduce((s, l) => s + l.amount.amount, 0) * 100,
  ) / 100;

describe("buildResaleJournal — Principal 10 → 13 (the bug fix)", () => {
  const j = buildResaleJournal({ sellGross: 13, buyGross: 10 });

  it("sell side totals 13.00 and is balanced (not 14.30)", () => {
    expect(sumEntry(j.lines, "Sell", "Debit")).toBe(13);
    expect(sumEntry(j.lines, "Sell", "Credit")).toBe(13);
  });

  it("buy side totals 10.00 and is balanced", () => {
    expect(sumEntry(j.lines, "Buy", "Debit")).toBe(10);
    expect(sumEntry(j.lines, "Buy", "Credit")).toBe(10);
  });

  it("headline total = sell side (13.00), never 14.30", () => {
    expect(j.totalDebits.amount).toBe(13);
    expect(j.totalCredits.amount).toBe(13);
    expect(j.totalDebits.amount).not.toBe(14.3);
  });

  it("books net + VAT control lines (11.30 / 1.70 sell, 8.70 / 1.30 buy)", () => {
    const find = (account: string, dir: "Debit" | "Credit") =>
      j.lines.find((l) => l.account === account && l.direction === dir)?.amount.amount;
    expect(find("MerchantReceivable", "Debit")).toBe(13);
    expect(find("RevenueNetSell", "Credit")).toBe(11.3);
    expect(find("VatOutput", "Credit")).toBe(1.7);
    expect(find("PartnerCost", "Debit")).toBe(8.7);
    expect(find("VatInput", "Debit")).toBe(1.3);
    expect(find("PartnerPayable", "Credit")).toBe(10);
  });

  it("derives margin 2.60 and net VAT 0.40 as summary figures (not journal lines)", () => {
    const s = deriveSettlementSummary(j);
    expect(s.sellPrice.amount).toBe(13);
    expect(s.buyPrice.amount).toBe(10);
    expect(s.margin).toBe(2.6);
    expect(s.netVatToZatca).toBe(0.4);
    // No line account literally named "ShippingMarginRevenue" inflating the total.
    expect(j.lines.some((l) => l.account === "ShippingMarginRevenue")).toBe(false);
  });
});

describe("buildResaleJournal — Principal 70 → 100", () => {
  const j = buildResaleJournal({ sellGross: 100, buyGross: 70 });
  it("sell side totals 100, buy side 70", () => {
    expect(sumEntry(j.lines, "Sell", "Debit")).toBe(100);
    expect(sumEntry(j.lines, "Buy", "Credit")).toBe(70);
  });
  it("derives sell 100 / buy 70 via the summary", () => {
    const s = deriveSettlementSummary(j);
    expect(s.sellPrice.amount).toBe(100);
    expect(s.buyPrice.amount).toBe(70);
  });
});

describe("reversal mirrors cleanly", () => {
  const j = buildResaleJournal({ sellGross: 13, buyGross: 10 });
  const r = invertJournal(j);
  it("preserves entry grouping and stays balanced", () => {
    const debit = r.lines.filter((l) => l.direction === "Debit").reduce((s, l) => s + l.amount.amount, 0);
    const credit = r.lines.filter((l) => l.direction === "Credit").reduce((s, l) => s + l.amount.amount, 0);
    expect(Math.round(debit * 100) / 100).toBe(Math.round(credit * 100) / 100);
    expect(r.lines.every((l) => l.entry === "Sell" || l.entry === "Buy")).toBe(true);
    // Sell side flipped: MerchantReceivable now a credit.
    expect(r.lines.find((l) => l.account === "MerchantReceivable")?.direction).toBe("Credit");
  });
});
