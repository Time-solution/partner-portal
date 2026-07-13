import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { createSeedData } from "@/lib/data/fixtures";
import { rolePermissionMap, PortalPermissions, type PortalRole } from "@/lib/rbac/portalRoles";
import { FINANCE_WORKSPACE_TABS } from "@/lib/rbac/partnerModules";
import {
  listAggregatorStatements,
  resetAggregatorStatements,
} from "@/lib/reconcile/aggregatorStatements";

vi.mock("@/features/auth/usePortalSession", () => ({
  usePortalSession: () => ({
    can: () => true,
    orgUser: { id: "ou-platform-accountant", name: "Omar Finance" },
  }),
}));

import { AggregatorReconcilePage } from "./AggregatorReconcilePage";

function stubLocalStorage() {
  const bag = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => bag.get(k) ?? null,
    setItem: (k: string, v: string) => bag.set(k, v),
    removeItem: (k: string) => bag.delete(k),
    clear: () => bag.clear(),
    key: () => null,
    length: 0,
  });
}

describe("AggregatorReconcilePage (mock store; compute-only)", () => {
  beforeEach(() => {
    stubLocalStorage();
    resetAggregatorStatements();
  });

  it("renders the statements list RTL in Arabic with the seeded Jahez statement and its open-exception count", () => {
    const html = renderToStaticMarkup(<AggregatorReconcilePage lang="ar" />);

    expect(html).toContain('data-testid="agg-rec-list"');
    expect(html).toContain('dir="rtl"');
    expect(html).toContain("Jahez");
    expect(html).toContain('data-testid="date-range-filter"'); // DateRangeFilter reused
    expect(html).toContain("agg-rec-open-agg-stmt-jahez-2026-06");
    // 4 open exceptions on the seeded statement surface in the list.
    expect(html).toContain(">4<");
  });

  it("registers the reconcile tab on the finance workspace (no new routing primitives)", () => {
    const tab = FINANCE_WORKSPACE_TABS.find((t) => t.id === "reconcile");
    expect(tab).toBeTruthy();
    expect(tab!.path).toBe("reconcile");
    expect(tab!.labelKey).toBe("financeTab_aggregatorReconcile");
  });
});

describe("structural scope — partner/merchant surfaces carry NO reconciliation data (6b standard)", () => {
  beforeEach(() => {
    stubLocalStorage();
    resetAggregatorStatements();
  });

  /** Keys that identify aggregator-statement reconciliation data. */
  const FORBIDDEN_KEYS = [
    "importKey",
    "importIdempotencyKey",
    "declaredGross",
    "declaredFees",
    "declaredNet",
    "aggregatorFee",
    "statementLineId",
    "resolutionNote",
    "exceptions",
  ] as const;

  function assertNoForbiddenKeysDeep(value: unknown, path = "root"): void {
    if (value == null || typeof value !== "object") return;
    for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
      expect(
        (FORBIDDEN_KEYS as readonly string[]).includes(key),
        `forbidden reconciliation key "${key}" found at ${path}.${key}`,
      ).toBe(false);
      assertNoForbiddenKeysDeep(child, `${path}.${key}`);
    }
  }

  it("the populated statement store never leaks into the portal seed data served to partner/merchant views", () => {
    // POPULATED fixture — the statement store holds the seeded Jahez statement with real amounts,
    // so a leak WOULD surface (not a vacuous pass).
    const statements = listAggregatorStatements();
    expect(statements[0].declaredNet).toBe(709.0);
    expect(statements[0].exceptions.length).toBeGreaterThan(0);

    // Deep key-level absence on the object graph partner/merchant surfaces consume.
    assertNoForbiddenKeysDeep(createSeedData());
  });

  it("only Accountant/PlatformAdmin hold Settlement.Reconcile — no partner/merchant role can act", () => {
    // Partner roles keep their pre-existing OWN-scoped Settlement.Read views; the reconcile
    // COMMANDS (import/resolve/close) are gated by Settlement.Reconcile, which stays exclusive.
    const platformRoles: PortalRole[] = ["Accountant", "PlatformAdmin"];
    const otherRoles = (Object.keys(rolePermissionMap) as PortalRole[]).filter(
      (role) => !platformRoles.includes(role),
    );

    expect(otherRoles.length).toBeGreaterThan(0);
    for (const role of otherRoles) {
      expect(rolePermissionMap[role]).not.toContain(PortalPermissions.Settlement.Reconcile);
    }
    for (const role of platformRoles) {
      expect(rolePermissionMap[role]).toContain(PortalPermissions.Settlement.Reconcile);
    }
  });
});
