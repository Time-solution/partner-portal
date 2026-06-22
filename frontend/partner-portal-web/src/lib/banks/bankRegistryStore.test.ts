import { describe, it, expect, beforeEach, vi } from "vitest";
import {
  createBankAccount,
  listBankAccounts,
  resetBankRegistry,
  setBankAccountStatus,
  updateBankAccount,
} from "./bankRegistryStore";

describe("bank registry store (Track B, mock)", () => {
  beforeEach(() => {
    const store: Record<string, string> = {};
    vi.stubGlobal("localStorage", {
      getItem: (k: string) => store[k] ?? null,
      setItem: (k: string, v: string) => {
        store[k] = v;
      },
      removeItem: (k: string) => {
        delete store[k];
      },
    });
    resetBankRegistry();
  });

  it("seeds two demo banks at 1101 and 1102", () => {
    const list = listBankAccounts();
    expect(list.map((b) => b.code)).toEqual(["1101", "1102"]);
  });

  it("adds a bank with the next sequential 110x sub-account", () => {
    const created = createBankAccount({ name: "Riyad Bank", accountNumber: "SA0000000000000000009999", currency: "SAR" });
    expect(created.code).toBe("1103");
    expect(created.status).toBe("Active");
    expect(listBankAccounts().map((b) => b.code)).toEqual(["1101", "1102", "1103"]);
  });

  it("updates and toggles status", () => {
    const created = createBankAccount({ name: "Temp", accountNumber: "1234567890", currency: "SAR" });
    const renamed = updateBankAccount(created.id, {
      name: "Renamed Bank",
      accountNumber: "1234567890",
      currency: "SAR",
      gatewayMapping: "gw-x",
    });
    expect(renamed.name).toBe("Renamed Bank");
    expect(renamed.gatewayMapping).toBe("gw-x");

    const off = setBankAccountStatus(created.id, "Inactive");
    expect(off.status).toBe("Inactive");
    const on = setBankAccountStatus(created.id, "Active");
    expect(on.status).toBe("Active");
  });
});
