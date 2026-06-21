import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { translations } from "@/lib/i18n";
import { FINANCE_WORKSPACE_TABS } from "@/lib/rbac/partnerModules";
import { isPathActive } from "@/lib/ui/tabs";
import { adminNav } from "./nav";

const REPORTS_PATH = "/finance/reports";
const sidebarReports = adminNav.find((i) => i.key === "reports")!;
const tabReports = FINANCE_WORKSPACE_TABS.find((t) => t.id === "reports")!;
const tabReportsPath = `/finance/${tabReports.path}`;

describe("Reports nav — single destination (sidebar deep-links into the finance tab)", () => {
  it("sidebar entry and finance tab point at the SAME route", () => {
    expect(sidebarReports.path).toBe(REPORTS_PATH);
    expect(tabReportsPath).toBe(REPORTS_PATH);
    expect(sidebarReports.path).toBe(tabReportsPath);
  });

  it("there is exactly one /finance route registration (no second Reports route)", () => {
    const routes = readFileSync("src/features/admin/AdminRoutes.tsx", "utf8");
    // One workspace route owns all of /finance/*; nothing registers /finance/reports on its own.
    expect((routes.match(/path="\/finance\/\*"/g) ?? []).length).toBe(1);
    expect(routes).not.toMatch(/path="\/finance\/reports"/);
  });

  it("the workspace renders ReportsPage exactly once at the single nested route", () => {
    const workspace = readFileSync("src/features/admin/pages/FinanceWorkspacePage.tsx", "utf8");
    expect((workspace.match(/<ReportsPage\b/g) ?? []).length).toBe(1);
    expect((workspace.match(/path="reports"/g) ?? []).length).toBe(1);
  });
});

describe("Reports nav — one label source (التقارير / Reports)", () => {
  it("sidebar and tab share the SAME i18n key", () => {
    expect(sidebarReports.labelKey).toBe("navReports");
    expect(tabReports.labelKey).toBe("navReports");
    expect(sidebarReports.labelKey).toBe(tabReports.labelKey);
  });

  it("label is identical in both languages", () => {
    expect(translations.ar.navReports).toBe("التقارير");
    expect(translations.en.navReports).toBe("Reports");
  });
});

describe("Reports nav — active state correct from both entry points", () => {
  const financeItem = adminNav.find((i) => i.key === "finance")!;
  const overviewTabPath = "/finance/overview";

  it("on /finance/reports: Reports sidebar item + Reports tab are 'here', Finance is not", () => {
    expect(isPathActive(REPORTS_PATH, sidebarReports.path)).toBe(true); // sidebar Reports
    expect(isPathActive(REPORTS_PATH, tabReportsPath)).toBe(true); // Finance Reports tab
    // No stale double-highlight: the sibling Finance entry / Overview tab stay inactive.
    expect(isPathActive(REPORTS_PATH, financeItem.path)).toBe(false);
    expect(isPathActive(REPORTS_PATH, overviewTabPath)).toBe(false);
  });

  it("on /finance/overview: Finance + Overview are 'here', Reports is not", () => {
    expect(isPathActive(overviewTabPath, financeItem.path)).toBe(true);
    expect(isPathActive(overviewTabPath, overviewTabPath)).toBe(true);
    expect(isPathActive(overviewTabPath, sidebarReports.path)).toBe(false);
    expect(isPathActive(overviewTabPath, tabReportsPath)).toBe(false);
  });

  it("active state is identical regardless of how Reports was reached (location-driven)", () => {
    // Whether navigated via the sidebar link or via the tab, the URL is the same, so the
    // computed active state is the same — there is no entry-point-dependent stale state.
    const viaSidebar = REPORTS_PATH;
    const viaTab = tabReportsPath;
    expect(viaSidebar).toBe(viaTab);
    expect(isPathActive(viaSidebar, sidebarReports.path)).toBe(isPathActive(viaTab, sidebarReports.path));
    expect(isPathActive(viaSidebar, tabReportsPath)).toBe(isPathActive(viaTab, tabReportsPath));
  });
});
