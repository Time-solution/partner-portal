import { describe, it, expect } from "vitest";
import {
  GATEWAY_AUTO_ROUTE_WIRED,
  isBankSubAccount,
  maskAccountNumber,
  nextBankCode,
  resolveGatewayBankCode,
  type BankAccount,
} from "./bankAccount";

describe("bank sub-account coding (Track B)", () => {
  it("assigns 1101, then 1102, then 1103", () => {
    expect(nextBankCode([])).toBe("1101");
    expect(nextBankCode(["1101"])).toBe("1102");
    expect(nextBankCode(["1101", "1102"])).toBe("1103");
  });

  it("reuses the lowest free gap", () => {
    expect(nextBankCode(["1102"])).toBe("1101");
    expect(nextBankCode(["1101", "1103"])).toBe("1102");
  });

  it("recognizes only the reserved 1101–1149 range as bank sub-accounts", () => {
    expect(isBankSubAccount("1101")).toBe(true);
    expect(isBankSubAccount("1149")).toBe(true);
    expect(isBankSubAccount("1100")).toBe(false); // the parent is not a sub-account
    expect(isBankSubAccount("1200")).toBe(false);
    expect(isBankSubAccount("1150")).toBe(false);
    expect(isBankSubAccount(undefined)).toBe(false);
  });
});

describe("account-number masking", () => {
  it("keeps only the last 4 characters", () => {
    expect(maskAccountNumber("SA4420000001234567891234")).toBe("••••••••••••••••••••1234");
    expect(maskAccountNumber("1234567890")).toBe("••••••7890");
    expect(maskAccountNumber("90")).toBe("90");
  });
});

describe("gateway auto-route seam (NOT wired)", () => {
  const banks: BankAccount[] = [
    { id: "a", code: "1101", name: "NCB", accountNumber: "1", currency: "SAR", status: "Active", gatewayMapping: "gw-ncb" },
    { id: "b", code: "1102", name: "Rajhi", accountNumber: "2", currency: "SAR", status: "Active", gatewayMapping: "gw-rajhi" },
  ];

  it("is flagged off", () => {
    expect(GATEWAY_AUTO_ROUTE_WIRED).toBe(false);
  });

  it("resolves a mapping (pure lookup) but is otherwise inert", () => {
    expect(resolveGatewayBankCode("gw-rajhi", banks)).toBe("1102");
    expect(resolveGatewayBankCode("unknown", banks)).toBeNull();
    expect(resolveGatewayBankCode(undefined, banks)).toBeNull();
  });
});
