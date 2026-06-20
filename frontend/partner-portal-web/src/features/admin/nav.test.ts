import { describe, expect, it } from "vitest";
import { PortalPermissions as PP } from "@/lib/rbac/portalRoles";
import { adminNav, filterNavByPermissions } from "./nav";

describe("filterNavByPermissions", () => {
  it("hides all sections when no permissions granted", () => {
    const keys = filterNavByPermissions(adminNav, []).map((item) => item.key);
    expect(keys).toEqual([]);
  });

  it("shows Partner Success Manager sections", () => {
    const granted = [
      PP.Dashboard.Read,
      PP.Partners.Read,
      PP.Partners.Manage,
      PP.Catalog.Read,
      PP.Activations.Read,
      PP.Activations.Request,
      PP.Reflection.Read,
      PP.Webhooks.Read,
      PP.Webhooks.Manage,
      PP.Credentials.Read,
    ];
    const keys = filterNavByPermissions(adminNav, granted).map((item) => item.key);
    expect(keys).toEqual([
      "dashboard",
      "partners",
      "catalog",
      "activations",
      "reflected-orders",
      "webhooks",
      "credentials",
    ]);
  });

  it("shows every item to Platform Admin (all portal permissions)", () => {
    const all = Object.values(PP).flatMap((g) => Object.values(g));
    const keys = filterNavByPermissions(adminNav, all).map((item) => item.key);
    expect(keys).toEqual(adminNav.map((item) => item.key));
  });

  it("Accountant sees settlement and billing but not settings manage-only paths via nav", () => {
    const granted = [
      PP.Dashboard.Read,
      PP.Partners.Read,
      PP.Catalog.Read,
      PP.Activations.Read,
      PP.Settlement.Read,
      PP.Billing.Read,
      PP.Reversals.Read,
      PP.Webhooks.Read,
      PP.Credentials.Read,
      PP.Settings.Read,
    ];
    const keys = filterNavByPermissions(adminNav, granted).map((item) => item.key);
    expect(keys).toContain("settlement");
    expect(keys).toContain("billing");
    expect(keys).not.toContain("reflected-orders");
  });
});
