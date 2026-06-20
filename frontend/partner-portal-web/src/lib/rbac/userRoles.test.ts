import { describe, expect, it } from "vitest";
import { canGrantRole, roleRequiresPartner } from "@/lib/rbac/portalRoles";

describe("user role helpers", () => {
  it("PartnerFinance requires partner assignment", () => {
    expect(roleRequiresPartner("PartnerFinance")).toBe(true);
    expect(roleRequiresPartner("PlatformAdmin")).toBe(false);
  });

  it("PlatformAdmin can grant any portal role", () => {
    expect(canGrantRole("PlatformAdmin", "PlatformAdmin")).toBe(true);
    expect(canGrantRole("PlatformAdmin", "PartnerFinance")).toBe(true);
  });

  it("lower roles cannot grant higher roles", () => {
    expect(canGrantRole("Accountant", "PlatformAdmin")).toBe(false);
    expect(canGrantRole("PartnerFinance", "Accountant")).toBe(false);
    expect(canGrantRole("Accountant", "Accountant")).toBe(true);
  });
});
