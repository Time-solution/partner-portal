import { ReportAccount } from "@/lib/reports/settlementReports";

/**
 * Track B — accountant-managed bank registry (mock). Banks are master data the accountant adds via
 * settings; each bank binds to a ledger sub-account code (1101, 1102, …) BENEATH the 1100 "Cash / Bank"
 * parent. This mirrors the backend `BankLedgerCoding` scheme. The frontend chart is name-based, so the
 * 1100 parent maps to `ReportAccount.Cash`; bank sub-accounts use their numeric 110x code as the id.
 *
 * MOCK/FLAGGED: nothing here mutates the production chart of accounts or the trial-balance money math —
 * the live chart change stays gated (CTO/accountant sign-off, same as 1250 / backend flag).
 */

export type BankAccountStatus = "Active" | "Inactive";

export interface BankAccount {
  id: string;
  /** Ledger sub-account code (1101–1149) under the 1100 parent. */
  code: string;
  name: string;
  /** Full account number / IBAN — stored whole, shown MASKED (see maskAccountNumber). */
  accountNumber: string;
  currency: string;
  status: BankAccountStatus;
  /** Optional future gateway-route key — SEAM ONLY, not wired to any live gateway. */
  gatewayMapping?: string;
}

export interface BankAccountInput {
  name: string;
  accountNumber: string;
  currency: string;
  gatewayMapping?: string;
}

/** The 1100 parent (name-based) that bank sub-accounts roll up to in the frontend chart. */
export const BANK_PARENT_ACCOUNT = ReportAccount.Cash;

/** The numeric parent code shown in the registry UI ("Bank / Cash Clearing"). */
export const BANK_PARENT_CODE = "1100";

export const BANK_SUBCODE_FIRST = 1101;
export const BANK_SUBCODE_LAST = 1149;

/** True for a reserved bank sub-account code (1101–1149) beneath the 1100 parent. */
export function isBankSubAccount(code: string | undefined | null): boolean {
  if (!code || code.length !== 4) return false;
  const n = Number(code);
  return Number.isInteger(n) && n >= BANK_SUBCODE_FIRST && n <= BANK_SUBCODE_LAST;
}

/** Next free sub-code (1101, 1102, …) given the codes in use; reuses the lowest gap first. */
export function nextBankCode(existingCodes: readonly string[]): string {
  const used = new Set<number>();
  for (const c of existingCodes) {
    const n = Number(c);
    if (Number.isInteger(n)) used.add(n);
  }
  for (let n = BANK_SUBCODE_FIRST; n <= BANK_SUBCODE_LAST; n++) {
    if (!used.has(n)) return String(n);
  }
  throw new Error("Bank sub-account range (1101–1149) exhausted.");
}

/** Mask an account number / IBAN for display — keep the last 4 characters, mask the rest. */
export function maskAccountNumber(accountNumber: string | undefined | null): string {
  const value = (accountNumber ?? "").trim();
  if (value.length <= 4) return value;
  return "•".repeat(value.length - 4) + value.slice(-4);
}

/**
 * GATEWAY AUTO-ROUTE — SEAM ONLY (not wired). A future gateway payment WOULD auto-locate its mapped
 * bank via `gatewayMapping` and auto-reconcile. This pure lookup proves the seam is reachable; there is
 * NO real gateway integration here (that is the separately-gated gateway phase).
 */
export const GATEWAY_AUTO_ROUTE_WIRED = false;

export function resolveGatewayBankCode(
  gatewayKey: string | undefined | null,
  banks: readonly BankAccount[],
): string | null {
  const key = (gatewayKey ?? "").trim();
  if (!key) return null;
  const match = banks.find(
    (b) => (b.gatewayMapping ?? "").trim().toLowerCase() === key.toLowerCase(),
  );
  return match ? match.code : null;
}
