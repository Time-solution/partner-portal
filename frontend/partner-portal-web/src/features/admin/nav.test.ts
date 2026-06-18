import { describe, expect, it } from "vitest";
import { ADMIN_PERMISSION, Permissions } from "@/lib/permissions";
import { adminNav, filterNavByPermissions } from "./nav";

describe("filterNavByPermissions", () => {
  it("shows permission-free items but hides guarded ones for a bare user", () => {
    const keys = filterNavByPermissions(adminNav, []).map((item) => item.key);
    expect(keys).toContain("dashboard");
    expect(keys).not.toContain("catalog");
    expect(keys).not.toContain("audit");
  });

  it("shows only the items a PartnerOps operator may see", () => {
    const granted = [
      Permissions.Catalog.Read,
      Permissions.Orders.Read,
      Permissions.Partners.Manage,
      Permissions.Roles.Manage,
    ];
    const keys = filterNavByPermissions(adminNav, granted).map((item) => item.key);
    expect(keys).toEqual(["dashboard", "catalog", "orders", "partners", "roles"]);
  });

  it("shows every item to an Admin super-user", () => {
    const keys = filterNavByPermissions(adminNav, [ADMIN_PERMISSION]).map((item) => item.key);
    expect(keys).toEqual(adminNav.map((item) => item.key));
  });
});
