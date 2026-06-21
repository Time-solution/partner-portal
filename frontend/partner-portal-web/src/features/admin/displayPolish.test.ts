import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { translations } from "@/lib/i18n";
import { isPathActive } from "@/lib/ui/tabs";
import { drillContextText, drillScopeLabel } from "@/lib/ui/drillContext";
import { EmptyState, TableEmptyRow } from "./components/EmptyState";

const read = (rel: string) => readFileSync(rel, "utf8");

describe("empty states — flagged list pages are never blank when empty", () => {
  const cases: { file: string; component: string; key: string }[] = [
    { file: "src/features/admin/pages/ReflectedOrdersPage.tsx", component: "TableEmptyRow", key: "reflectedEmpty" },
    { file: "src/features/admin/pages/CatalogPage.tsx", component: "TableEmptyRow", key: "catalogEmpty" },
    { file: "src/features/admin/pages/MenuPage.tsx", component: "EmptyState", key: "menuEmpty" },
    { file: "src/features/admin/pages/ActivationsPage.tsx", component: "EmptyState", key: "activationsEmpty" },
    { file: "src/features/admin/pages/BillingPage.tsx", component: "TableEmptyRow", key: "billingPeriodsEmpty" },
  ];

  it("the shared empty-state helpers exist", () => {
    expect(typeof EmptyState).toBe("function");
    expect(typeof TableEmptyRow).toBe("function");
  });

  for (const { file, component, key } of cases) {
    it(`${file.split("/").pop()} renders an empty branch (${component} + ${key})`, () => {
      const src = read(file);
      expect(src, `${file} must use ${component}`).toContain(component);
      expect(src, `${file} must show the ${key} message`).toContain(key);
      // The empty branch must be guarded by a length check (not a permanently blank table/list).
      expect(src).toMatch(/\.length === 0/);
    });
  }

  it("BillingPage distinguishes invoices-only empty copy", () => {
    expect(read("src/features/admin/pages/BillingPage.tsx")).toContain("invoicesEmpty");
  });

  it("every empty-state message is bilingual (AR + EN)", () => {
    for (const key of ["reflectedEmpty", "catalogEmpty", "menuEmpty", "activationsEmpty", "billingPeriodsEmpty", "invoicesEmpty"] as const) {
      expect(translations.ar[key], `AR missing ${key}`).toBeTruthy();
      expect(translations.en[key], `EN missing ${key}`).toBeTruthy();
      expect(translations.ar[key]).not.toBe(translations.en[key]);
    }
  });
});

describe("active-state — one shared isPathActive predicate across tabs/subtabs", () => {
  it("module subtab is 'here' for its own route and descendants, not siblings", () => {
    const reflected = "/modules/commerce/reflected";
    expect(isPathActive(reflected, reflected)).toBe(true);
    expect(isPathActive(`${reflected}/ord-1`, reflected)).toBe(true);
    expect(isPathActive("/modules/commerce/catalog", reflected)).toBe(false);
  });

  it("partner subtab is 'here' for its own route only", () => {
    const tab = "/partners/p-1/reflected";
    expect(isPathActive(tab, tab)).toBe(true);
    expect(isPathActive("/partners/p-1/catalog", tab)).toBe(false);
  });

  it("the shared subnav components use isPathActive (no parallel active-state path)", () => {
    for (const file of [
      "src/features/admin/components/ModuleSubNav.tsx",
      "src/features/admin/components/PartnerSubNav.tsx",
    ]) {
      const src = read(file);
      expect(src, `${file} must use the shared predicate`).toContain("isPathActive(pathname");
      // Must NOT fall back to NavLink's own isActive callback (that would be a second path).
      expect(src, `${file} must not use NavLink isActive callback`).not.toMatch(/\(\{\s*isActive\s*\}\)/);
      // Active styling still comes from the single token source.
      expect(src).toContain("tabLinkClass");
    }
  });
});

describe("context clarity — drill-down shows whose data + which period", () => {
  const PLATFORM = "All partners & merchants";

  it("platform / no-org views show the aggregate scope", () => {
    expect(drillScopeLabel({ level: "Platform", name: "HQ" }, PLATFORM)).toBe(PLATFORM);
    expect(drillScopeLabel(null, PLATFORM)).toBe(PLATFORM);
    expect(drillScopeLabel(undefined, PLATFORM)).toBe(PLATFORM);
  });

  it("partner / merchant views show their own entity name", () => {
    expect(drillScopeLabel({ level: "Partner", name: "The Chefz" }, PLATFORM)).toBe("The Chefz");
    expect(drillScopeLabel({ level: "Merchant", name: "Shawarma House" }, PLATFORM)).toBe("Shawarma House");
  });

  it("falls back to the aggregate label when a scoped org has no name", () => {
    expect(drillScopeLabel({ level: "Partner", name: "  " }, PLATFORM)).toBe(PLATFORM);
  });

  it("context text combines entity + period (with an all-periods fallback)", () => {
    expect(drillContextText("The Chefz", "2026-06", "All periods")).toBe("The Chefz · 2026-06");
    expect(drillContextText(PLATFORM, "", "All periods")).toBe(`${PLATFORM} · All periods`);
  });

  it("context labels are defined in both languages", () => {
    for (const key of ["drillContextLabel", "drillScopePlatform", "drillAllPeriods"] as const) {
      expect(translations.ar[key]).toBeTruthy();
      expect(translations.en[key]).toBeTruthy();
    }
    // The drill-down renders the context header (guards the 'same screen, two contexts' finding).
    expect(read("src/features/admin/components/FinanceDrillDown.tsx")).toContain("drillContextLabel");
  });
});
