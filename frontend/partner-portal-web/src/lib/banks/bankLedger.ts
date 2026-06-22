import type { Money, SettlementJournalLine } from "@/lib/data/types";
import { ReportAccount } from "@/lib/reports/settlementReports";
import { BANK_PARENT_ACCOUNT, BANK_PARENT_CODE, isBankSubAccount, type BankAccount } from "./bankAccount";

/**
 * Track B — COMPUTE-ONLY per-bank ledger helpers (mock). These DISPLAY the bank routing of a recorded
 * payment and roll per-bank balances up to the 1100 parent. They do NOT feed the trial-balance money
 * math in `settlementReports.ts` (which is untouched), so the trial balance still nets to zero.
 */

function r2(n: number): number {
  return Math.round(n * 100) / 100;
}

/**
 * The journal a payment-received WOULD post when routed to a chosen bank:
 *   Dr {bank sub-account 110x | 1100 Cash parent}  /  Cr {receivable}.
 * Display only — never persisted into the trial balance while the live chart change is gated.
 */
export function buildPaymentReceivedJournal(args: {
  amount: number;
  currency: string;
  /** Chosen bank sub-account code (110x); when absent/invalid the cash falls back to the 1100 parent. */
  bankCode?: string;
  receivableAccount?: string;
}): SettlementJournalLine[] {
  const amount = r2(args.amount);
  const money: Money = { amount, currency: args.currency, vatInclusive: true };
  const bankAccount = isBankSubAccount(args.bankCode) ? args.bankCode! : BANK_PARENT_ACCOUNT;
  const receivable = args.receivableAccount ?? ReportAccount.MerchantReceivable;
  return [
    { account: bankAccount, direction: "Debit", amount: { ...money }, entry: "PaymentReceived" },
    { account: receivable, direction: "Credit", amount: { ...money }, entry: "PaymentReceived" },
  ];
}

export interface BankPaymentRef {
  /** The BankAccount.id the payment landed in; absent → un-routed (1100 parent). */
  bankAccountId?: string;
  amount: number;
}

export interface BankLedgerRow {
  code: string;
  name: string;
  total: number;
}

export interface BankLedgerRollup {
  perBank: BankLedgerRow[];
  /** Cash booked directly to the 1100 parent (un-routed payments). */
  parentDirect: number;
  /** Parent total = direct + Σ children (the roll-up invariant). */
  rollupTotal: number;
}

/**
 * Per-bank balances from recorded payments, rolled up to the 1100 parent. A payment with a known
 * bankAccountId lands in that bank's sub-account; anything else falls back to the parent. The roll-up
 * total always equals parentDirect + Σ(perBank), so 1100 = sum of its children.
 */
export function bankBalances(
  payments: readonly BankPaymentRef[],
  banks: readonly BankAccount[],
): BankLedgerRollup {
  const byId = new Map(banks.map((b) => [b.id, b]));
  const totals = new Map<string, number>();
  let parentDirect = 0;

  for (const p of payments) {
    const bank = p.bankAccountId ? byId.get(p.bankAccountId) : undefined;
    if (bank) {
      totals.set(bank.id, (totals.get(bank.id) ?? 0) + p.amount);
    } else {
      parentDirect += p.amount;
    }
  }

  const perBank: BankLedgerRow[] = banks
    .map((b) => ({ code: b.code, name: b.name, total: r2(totals.get(b.id) ?? 0) }))
    .sort((a, b) => a.code.localeCompare(b.code));

  parentDirect = r2(parentDirect);
  const rollupTotal = r2(parentDirect + perBank.reduce((s, b) => s + b.total, 0));

  return { perBank, parentDirect, rollupTotal };
}

export { BANK_PARENT_CODE };
