import { describe, expect, it } from "vitest";
import { PortalPermissions as PP } from "@/lib/rbac/portalRoles";
import { adminNav, filterNavByPermissions, navForRole } from "./nav";
import {
  MOCK_PARTNER_FINANCE_PARTNER_ID,
  MOCK_PARTNER_PSM_PARTNER_ID,
  ROLE_NAV_KEYS,
  defaultLandingPath,
  isPathAllowedForRole,
} from "@/lib/rbac/roleNavConfig";

describe("filterNavByPermissions", () => {
  it("hides all sections when no permissions granted", () => {
    const keys = filterNavByPermissions(adminNav, []).map((item) => item.key);
    expect(keys).toEqual([]);
  });

  it("Partner Success Manager sees partner home only (no platform modules)", () => {
    const granted = [
      PP.Partners.Read,
      PP.Catalog.Read,
      PP.Activations.Read,
      PP.Activations.Request,
      PP.Reflection.Read,
      PP.Settlement.Read,
      PP.Reversals.Read,
      PP.Webhooks.Read,
      PP.Webhooks.Manage,
      PP.Credentials.Read,
    ];
    const keys = filterNavByPermissions(adminNav, granted, "PartnerSuccessManager").map(
      (item) => item.key,
    );
    expect(keys).toEqual(["partner-home", "webhooks", "credentials"]);
  });

  it("shows every item to Platform Admin (all portal permissions)", () => {
    const all = Object.values(PP).flatMap((g) => Object.values(g));
    const keys = filterNavByPermissions(adminNav, all, "PlatformAdmin").map((item) => item.key);
    expect(keys).toEqual([...ROLE_NAV_KEYS.PlatformAdmin]);
  });

  it("Accountant lands on finance workspace nav, not partner modules", () => {
    const granted = [
      PP.Finance.Read,
      PP.Settings.Read,
    ];
    const keys = filterNavByPermissions(adminNav, granted, "Accountant").map((item) => item.key);
    expect(keys).toEqual(["finance", "settings"]);
  });

  it("MerchantPreview role sees merchant preview nav only", () => {
    const keys = filterNavByPermissions(
      adminNav,
      [PP.MerchantPreview.Read],
      "MerchantPreview",
    ).map((item) => item.key);
    expect(keys).toEqual(["merchant-preview"]);
  });

  it("Partner Finance resolves partner home path to scoped partner id", () => {
    const items = navForRole(
      "PartnerFinance",
      [PP.PartnerFinance.ReadOwn, PP.Webhooks.Read, PP.Credentials.Read],
      MOCK_PARTNER_FINANCE_PARTNER_ID,
    );
    expect(items.find((i) => i.key === "partner-home")?.path).toBe(
      `/partners/${MOCK_PARTNER_FINANCE_PARTNER_ID}`,
    );
  });
});

describe("defaultLandingPath", () => {
  it("routes each role to its own home", () => {
    expect(defaultLandingPath("PlatformAdmin")).toBe("/dashboard");
    expect(defaultLandingPath("Accountant")).toBe("/finance/overview");
    expect(defaultLandingPath("MerchantPreview")).toBe("/merchant-preview");
    expect(defaultLandingPath("PartnerSuccessManager")).toBe(
      `/partners/${MOCK_PARTNER_PSM_PARTNER_ID}`,
    );
    expect(defaultLandingPath("PartnerFinance")).toBe(
      `/partners/${MOCK_PARTNER_FINANCE_PARTNER_ID}`,
    );
  });
});

describe("isPathAllowedForRole", () => {
  it("blocks partner roles from platform admin paths", () => {
    expect(isPathAllowedForRole("/dashboard", "PartnerSuccessManager")).toBe(false);
    expect(isPathAllowedForRole("/modules/delivery-service/catalog", "PartnerFinance")).toBe(
      false,
    );
    expect(isPathAllowedForRole("/partners", "PartnerFinance")).toBe(false);
  });

  it("allows partner roles on own partner home and integrations", () => {
    expect(
      isPathAllowedForRole(`/partners/${MOCK_PARTNER_PSM_PARTNER_ID}/catalog`, "PartnerSuccessManager"),
    ).toBe(true);
    expect(isPathAllowedForRole("/webhooks", "PartnerFinance")).toBe(true);
    expect(isPathAllowedForRole("/credentials", "PartnerFinance")).toBe(true);
  });

  it("merchant role is limited to merchant preview", () => {
    expect(isPathAllowedForRole("/merchant-preview", "MerchantPreview")).toBe(true);
    expect(isPathAllowedForRole("/dashboard", "MerchantPreview")).toBe(false);
  });
});
