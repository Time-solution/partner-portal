import { describe, it, expect } from "vitest";
import { ReportAccount } from "@/lib/reports/settlementReports";
import { bankBalances, buildPaymentReceivedJournal } from "./bankLedger";
import { BANK_PARENT_ACCOUNT, type BankAccount } from "./bankAccount";

const banks: BankAccount[] = [
  { id: "ncb", code: "1101", name: "NCB", accountNumber: "1", currency: "SAR", status: "Active" },
  { id: "rajhi", code: "1102", name: "Rajhi", accountNumber: "2", currency: "SAR", status: "Active" },
];

describe("buildPaymentReceivedJournal (compute-only, Track B)", () => {
  it("debits the chosen bank sub-account and credits the receivable", () => {
    const lines = buildPaymentReceivedJournal({ amount: 100, currency: "SAR", bankCode: "1101" });
    const debit = lines.find((l) => l.direction === "Debit")!;
    const credit = lines.find((l) => l.direction === "Credit")!;
    expect(debit.account).toBe("1101");
    expect(debit.amount.amount).toBe(100);
    expect(credit.account).toBe(ReportAccount.MerchantReceivable);
    expect(credit.amount.amount).toBe(100);
  });

  it("falls back to the 1100 (Cash) parent when no bank is chosen", () => {
    const lines = buildPaymentReceivedJournal({ amount: 60, currency: "SAR" });
    expect(lines.find((l) => l.direction === "Debit")!.account).toBe(BANK_PARENT_ACCOUNT);
  });

  it("is balanced (debits equal credits)", () => {
    const lines = buildPaymentReceivedJournal({ amount: 42.5, currency: "SAR", bankCode: "1102" });
    const debit = lines.filter((l) => l.direction === "Debit").reduce((s, l) => s + l.amount.amount, 0);
    const credit = lines.filter((l) => l.direction === "Credit").reduce((s, l) => s + l.amount.amount, 0);
    expect(debit).toBe(credit);
  });
});

describe("bankBalances rollup (Track B)", () => {
  it("reports per-bank totals and rolls up to the 1100 parent (parent = direct + Σ children)", () => {
    const rollup = bankBalances(
      [
        { bankAccountId: "ncb", amount: 60 },
        { bankAccountId: "rajhi", amount: 40 },
        { bankAccountId: "ncb", amount: 10 },
      ],
      banks,
    );

    expect(rollup.perBank.find((b) => b.code === "1101")!.total).toBe(70);
    expect(rollup.perBank.find((b) => b.code === "1102")!.total).toBe(40);
    expect(rollup.parentDirect).toBe(0);
    expect(rollup.rollupTotal).toBe(110);

    const childrenSum = rollup.perBank.reduce((s, b) => s + b.total, 0) + rollup.parentDirect;
    expect(rollup.rollupTotal).toBe(childrenSum);
  });

  it("routes un-mapped payments to the 1100 parent direct balance", () => {
    const rollup = bankBalances(
      [
        { bankAccountId: "ncb", amount: 60 },
        { bankAccountId: undefined, amount: 25 },
        { bankAccountId: "missing-bank", amount: 5 },
      ],
      banks,
    );

    expect(rollup.perBank.find((b) => b.code === "1101")!.total).toBe(60);
    expect(rollup.parentDirect).toBe(30); // 25 un-routed + 5 unknown bank
    expect(rollup.rollupTotal).toBe(90);
  });
});
