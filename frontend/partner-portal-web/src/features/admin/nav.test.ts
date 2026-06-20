import { describe, expect, it } from "vitest";
import { PortalPermissions as PP } from "@/lib/rbac/portalRoles";
import { adminNav, filterNavByPermissions } from "./nav";

describe("filterNavByPermissions", () => {
  it("hides all sections when no permissions granted", () => {
    const keys = filterNavByPermissions(adminNav, []).map((item) => item.key);
    expect(keys).toEqual([]);
  });

  it("shows Partner Success Manager module nav (not finance workspace)", () => {
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
    const keys = filterNavByPermissions(adminNav, granted, "PartnerSuccessManager").map(
      (item) => item.key,
    );
    expect(keys).toContain("dashboard");
    expect(keys).toContain("partners");
    expect(keys).toContain("delivery-service");
    expect(keys).toContain("commerce");
    expect(keys).not.toContain("finance");
  });

  it("shows every item to Platform Admin (all portal permissions)", () => {
    const all = Object.values(PP).flatMap((g) => Object.values(g));
    const keys = filterNavByPermissions(adminNav, all, "PlatformAdmin").map((item) => item.key);
    expect(keys).toEqual(adminNav.map((item) => item.key));
  });

  it("Accountant lands on finance workspace nav, not partner modules", () => {
    const granted = [
      PP.Dashboard.Read,
      PP.Finance.Read,
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
    const keys = filterNavByPermissions(adminNav, granted, "Accountant").map((item) => item.key);
    expect(keys).toContain("finance");
    expect(keys).not.toContain("delivery-service");
    expect(keys).not.toContain("commerce");
  });

  it("MerchantPreview role sees merchant preview nav only", () => {
    const keys = filterNavByPermissions(
      adminNav,
      ["Zahy.Portal.MerchantPreview.Read"],
      "MerchantPreview",
    ).map((item) => item.key);
    expect(keys).toEqual(["merchant-preview"]);
  });

  it("Platform Admin sees merchant preview in nav", () => {
    const all = Object.values(PP).flatMap((g) => Object.values(g));
    const keys = filterNavByPermissions(adminNav, all, "PlatformAdmin").map((item) => item.key);
    expect(keys).toContain("merchant-preview");
  });
});
