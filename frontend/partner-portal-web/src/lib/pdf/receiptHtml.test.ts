import { describe, expect, it } from "vitest";
import { RIYAL_SVG_PATHS } from "@/components/riyalSymbol";
import { moneyMarkup, MONEY_SYMBOL_GAP_EM } from "@/lib/format/moneyMarkup";
import type { BankAccount } from "@/lib/banks/bankAccount";
import {
  buildReceiptHtml,
  receiptBankLabel,
  receiptRiyalMarkup,
  RECEIPT_BETA_NOTICE_AR,
  RECEIPT_BETA_NOTICE_EN,
  type ReceiptDoc,
} from "./receiptHtml";

function arabicDoc(over: Partial<ReceiptDoc> = {}): ReceiptDoc {
  return {
    lang: "ar",
    receiptNo: "ZR-2026-0001",
    date: "2026-06-21",
    merchantName: "Al Rajhi Demo Store",
    invoiceRef: "ZH-202606-M099-0001",
    method: "Transfer",
    amount: 45,
    currency: "SAR",
    bankLabel: "Al Rajhi Bank · ••••6789",
    ...over,
  };
}

const banks: BankAccount[] = [
  { id: "bank-1", code: "1101", name: "Al Rajhi Bank", accountNumber: "SA0380000000123456789", currency: "SAR", status: "Active" },
  { id: "bank-2", code: "1102", name: "Riyad Bank", accountNumber: "SA90RY000099", currency: "SAR", status: "Active" },
];

describe("payment receipt HTML (the html2canvas layout — fixes mojibake, renders Riyal)", () => {
  it("renders connected Arabic labels (no mojibake) when lang = ar", () => {
    const html = buildReceiptHtml(arabicDoc());
    // real Arabic text, never the þ©þ•þª-style mojibake the jsPDF-core path produced
    expect(html).toContain("إيصال سداد");
    expect(html).toContain("رقم الإيصال");
    expect(html).toContain("المدفوع من");
    expect(html).toContain("المبلغ المستلم");
    expect(html).not.toContain("þ");
  });

  it("renders English labels when lang = en", () => {
    const html = buildReceiptHtml(arabicDoc({ lang: "en" }));
    expect(html).toContain("Payment receipt");
    expect(html).toContain("Receipt No");
    expect(html).toContain("Paid by");
    expect(html).toContain("Amount received");
  });

  it("renders the Riyal as a sharp inline SVG vector — never 'SAR' or a tofu glyph", () => {
    const html = buildReceiptHtml(arabicDoc());
    expect(receiptRiyalMarkup()).toContain("<svg");
    expect(html).toContain(RIYAL_SVG_PATHS[0].slice(0, 32));
    expect(html).not.toContain("SAR");
  });

  it("reuses the shared moneyMarkup — Riyal LEFT of the number with the one gap (single source)", () => {
    const html = buildReceiptHtml(arabicDoc());
    expect(html).toContain(moneyMarkup(45));
    expect(html).toContain(`dir="ltr" style="display:inline-flex;flex-direction:row`);
    expect(html).toContain(`gap:${MONEY_SYMBOL_GAP_EM}em`);
    const cell = moneyMarkup(45);
    expect(cell.indexOf("<svg")).toBeLessThan(cell.indexOf("45.00"));
  });

  it("AR/EN toggle switches direction: ar = RTL, en = LTR", () => {
    expect(buildReceiptHtml(arabicDoc({ lang: "ar" }))).toContain('dir="rtl"');
    expect(buildReceiptHtml(arabicDoc({ lang: "en" }))).toContain('dir="ltr"');
  });

  it("shows the bank / payment-destination field from the resolved label", () => {
    const html = buildReceiptHtml(arabicDoc({ bankLabel: "Al Rajhi Bank · ••••6789" }));
    expect(html).toContain("مستلَم في");
    expect(html).toContain("Al Rajhi Bank · ••••6789");
    const en = buildReceiptHtml(arabicDoc({ lang: "en" }));
    expect(en).toContain("Received to");
  });

  it("keeps the BETA footer — proof of payment, not a ZATCA tax invoice", () => {
    const ar = buildReceiptHtml(arabicDoc());
    expect(ar).toContain("BETA");
    expect(ar).toContain(RECEIPT_BETA_NOTICE_AR);
    expect(ar).toContain("إثبات دفع");
    const en = buildReceiptHtml(arabicDoc({ lang: "en" }));
    expect(en).toContain(RECEIPT_BETA_NOTICE_EN);
    expect(en.toLowerCase()).toContain("not a zatca tax invoice");
  });
});

describe("receiptBankLabel — resolves the Track B bank registry (display only)", () => {
  it("formats {bank name} · ••••{last4} when the payment carries a known bank", () => {
    expect(receiptBankLabel("bank-1", banks, "ar")).toBe("Al Rajhi Bank · ••••6789");
    expect(receiptBankLabel("bank-2", banks, "en")).toBe("Riyad Bank · ••••0099");
  });

  it("falls back to the 1100 parent label when no bank on the payment", () => {
    expect(receiptBankLabel(undefined, banks, "ar")).toContain("1100");
    expect(receiptBankLabel(undefined, banks, "en")).toContain("1100 parent");
  });

  it("falls back when the bank id is unknown (deactivated/removed)", () => {
    expect(receiptBankLabel("bank-zzz", banks, "en")).toContain("1100 parent");
  });
});
