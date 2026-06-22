import {
  nextBankCode,
  type BankAccount,
  type BankAccountInput,
  type BankAccountStatus,
} from "./bankAccount";

/**
 * Track B — sibling localStorage store for the accountant-managed bank registry (mock), mirroring the
 * `orgProfileStore` pattern. Kept SEPARATE from the main PortalData so the registry is platform-level
 * master data, not per-tenant scoped. No production chart is mutated.
 */

const STORAGE_KEY = "zahy-bank-registry-v1";

function seedDefaults(): BankAccount[] {
  return [
    {
      id: "bank-ncb",
      code: "1101",
      name: "البنك الأهلي السعودي",
      accountNumber: "SA0380000000608010167519",
      currency: "SAR",
      status: "Active",
      gatewayMapping: undefined,
    },
    {
      id: "bank-rajhi",
      code: "1102",
      name: "مصرف الراجحي",
      accountNumber: "SA4420000001234567891234",
      currency: "SAR",
      status: "Active",
      gatewayMapping: undefined,
    },
  ];
}

function loadList(): BankAccount[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return seedDefaults();
    const parsed = JSON.parse(raw) as BankAccount[];
    return Array.isArray(parsed) ? parsed : seedDefaults();
  } catch {
    return seedDefaults();
  }
}

function saveList(list: BankAccount[]): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
}

export function listBankAccounts(): BankAccount[] {
  return loadList()
    .slice()
    .sort((a, b) => a.code.localeCompare(b.code));
}

function uid(): string {
  return `bank-${Math.random().toString(36).slice(2, 10)}`;
}

export function createBankAccount(input: BankAccountInput): BankAccount {
  const list = loadList();
  const code = nextBankCode(list.map((b) => b.code));
  const bank: BankAccount = {
    id: uid(),
    code,
    name: input.name.trim(),
    accountNumber: input.accountNumber.trim(),
    currency: (input.currency || "SAR").trim(),
    status: "Active",
    gatewayMapping: input.gatewayMapping?.trim() || undefined,
  };
  saveList([...list, bank]);
  return bank;
}

export function updateBankAccount(id: string, input: BankAccountInput): BankAccount {
  const list = loadList();
  const idx = list.findIndex((b) => b.id === id);
  if (idx < 0) throw new Error("Bank account not found");
  const next: BankAccount = {
    ...list[idx],
    name: input.name.trim(),
    accountNumber: input.accountNumber.trim(),
    currency: (input.currency || "SAR").trim(),
    gatewayMapping: input.gatewayMapping?.trim() || undefined,
  };
  list[idx] = next;
  saveList(list);
  return next;
}

export function setBankAccountStatus(id: string, status: BankAccountStatus): BankAccount {
  const list = loadList();
  const idx = list.findIndex((b) => b.id === id);
  if (idx < 0) throw new Error("Bank account not found");
  list[idx] = { ...list[idx], status };
  saveList(list);
  return list[idx];
}

export function resetBankRegistry(): void {
  localStorage.removeItem(STORAGE_KEY);
}
