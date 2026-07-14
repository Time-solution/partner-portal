import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { renderToStaticMarkup } from "react-dom/server";
import { PortalPermissions as PP, rolePermissionMap } from "@/lib/rbac/portalRoles";
import {
  MOCK_PARTNER_PSM_PARTNER_ID,
  ROLE_NAV_KEYS,
  isPathAllowedForRole,
} from "@/lib/rbac/roleNavConfig";
import { navForRole } from "./nav";
import { parseMerchantTab } from "@/lib/rbac/merchantTabs";
import { Sheet } from "@/components/ui/sheet";
import { ErrorState } from "@/components/states";

describe("both sidebars — structural per-role separation", () => {
  it("merchant items and partner items are disjoint (structural, not hidden)", () => {
    const merchant = new Set(ROLE_NAV_KEYS.MerchantPreview);
    const partner = new Set(ROLE_NAV_KEYS.PartnerSuccessManager);
    for (const key of merchant) expect(partner.has(key)).toBe(false);
    expect(merchant.size).toBe(7);
    expect(ROLE_NAV_KEYS.PartnerSuccessManager).toEqual(ROLE_NAV_KEYS.PartnerFinance);
  });

  it("every merchant sidebar entry resolves to an allowed route AND a real tab (no orphans)", () => {
    const items = navForRole("MerchantPreview", [PP.MerchantPreview.Read]);
    expect(items.map((i) => i.key)).toEqual([...ROLE_NAV_KEYS.MerchantPreview]);
    for (const item of items) {
      const [pathname, query] = item.path.split("?");
      expect(isPathAllowedForRole(pathname, "MerchantPreview")).toBe(true);
      const tab = new URLSearchParams(query ?? "").get("tab");
      // Each entry lands on a real merchant view: the parsed tab round-trips (dashboard for none).
      expect(parseMerchantTab(tab)).toBe(tab ?? "dashboard");
    }
  });

  it("every partner sidebar entry resolves under the OWN partner scope (no orphans, no leakage)", () => {
    const items = navForRole(
      "PartnerSuccessManager",
      rolePermissionMap.PartnerSuccessManager,
      MOCK_PARTNER_PSM_PARTNER_ID,
    );
    expect(items.map((i) => i.key)).toEqual([...ROLE_NAV_KEYS.PartnerSuccessManager]);
    const partnerScreenPaths = new Set([
      "active-merchants",
      "catalog",
      "usage-packages",
      "activations",
      "orders",
      "statement",
      "profile",
    ]);
    for (const item of items) {
      expect(isPathAllowedForRole(item.path, "PartnerSuccessManager", MOCK_PARTNER_PSM_PARTNER_ID)).toBe(true);
      if (item.path.startsWith("/partners/")) {
        const [, , partnerId, screen] = item.path.split("/");
        expect(partnerId).toBe(MOCK_PARTNER_PSM_PARTNER_ID);
        expect(partnerScreenPaths.has(screen)).toBe(true);
      }
    }
  });

  it("no partner entry leaks into admin/merchant roles and vice versa", () => {
    const adminKeys = new Set(ROLE_NAV_KEYS.PlatformAdmin);
    for (const key of ROLE_NAV_KEYS.MerchantPreview) expect(adminKeys.has(key)).toBe(false);
    for (const key of ["partner-catalog", "partner-orders", "partner-settings"]) {
      expect(adminKeys.has(key)).toBe(false);
      expect(new Set(ROLE_NAV_KEYS.Accountant).has(key)).toBe(false);
    }
  });
});

describe("RTL", () => {
  it("the shell root mirrors with dir (sidebar sits on the inline-start side)", () => {
    const src = readFileSync(join(__dirname, "AdminShell.tsx"), "utf8");
    expect(src).toContain('dir={lang === "ar" ? "rtl" : "ltr"}');
    expect(src).toContain("border-e"); // logical property — mirrors under RTL
  });

  it("detail sheet renders dir=rtl for Arabic", () => {
    const html = renderToStaticMarkup(
      <Sheet open onClose={() => {}} title="عنوان" dir="rtl">
        <p>محتوى</p>
      </Sheet>,
    );
    expect(html).toContain('dir="rtl"');
  });
});

describe("shared error state", () => {
  it("renders the alert anatomy with a retry action", () => {
    const html = renderToStaticMarkup(
      <ErrorState message="تعذّر التحميل" retryLabel="إعادة المحاولة" onRetry={() => {}} />,
    );
    expect(html).toContain('role="alert"');
    expect(html).toContain("تعذّر التحميل");
    expect(html).toContain("إعادة المحاولة");
  });
});
