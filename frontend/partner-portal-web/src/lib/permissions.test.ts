import { describe, expect, it } from "vitest";
import {
  ADMIN_PERMISSION,
  Permissions,
  hasAnyPermission,
  hasPermission,
} from "./permissions";

describe("hasPermission", () => {
  it("returns true when the exact permission is granted", () => {
    expect(hasPermission([Permissions.Catalog.Read], Permissions.Catalog.Read)).toBe(true);
  });

  it("returns false when the permission is missing", () => {
    expect(hasPermission([Permissions.Orders.Read], Permissions.Catalog.Read)).toBe(false);
  });

  it("treats Admin as a super-power implying any permission", () => {
    expect(hasPermission([ADMIN_PERMISSION], Permissions.Payouts.Read)).toBe(true);
  });
});

describe("hasAnyPermission", () => {
  it("allows when no permission is required", () => {
    expect(hasAnyPermission([], [])).toBe(true);
  });

  it("matches on any-of", () => {
    expect(
      hasAnyPermission(
        [Permissions.Catalog.Write],
        [Permissions.Catalog.Read, Permissions.Catalog.Write],
      ),
    ).toBe(true);
  });

  it("denies when none of the required are granted", () => {
    expect(hasAnyPermission([Permissions.Orders.Read], [Permissions.Payouts.Read])).toBe(false);
  });

  it("treats Admin as a super-power", () => {
    expect(hasAnyPermission([ADMIN_PERMISSION], [Permissions.Webhooks.Manage])).toBe(true);
  });
});
