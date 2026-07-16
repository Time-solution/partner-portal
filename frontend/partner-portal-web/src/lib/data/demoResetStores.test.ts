import { beforeEach, describe, expect, it, vi } from "vitest";
import { MockPortalDataSource } from "./mockDataSource";
import { createSeedData } from "./fixtures";
import { savePersistedData } from "./mockStore";
import { saveListing } from "@/lib/catalog/listingStore";
import { createOrder, saveOrder, listOrdersForTenant } from "@/lib/orders/serviceOrders";
import { importAggregatorStatement, listAggregatorStatements } from "@/lib/reconcile/aggregatorStatements";

function stubLocalStorage() {
  const bag = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => bag.get(k) ?? null,
    setItem: (k: string, v: string) => void bag.set(k, v),
    removeItem: (k: string) => void bag.delete(k),
    clear: () => bag.clear(),
    key: () => null,
    length: 0,
  });
}

const STORE_KEYS = [
  "zahy-catalog-listings-v1",
  "zahy-service-orders-v1",
  "zahy-aggregator-statements-v1",
] as const;

describe("AF5 — the 3 sibling stores are wired into demo reset", () => {
  beforeEach(() => {
    stubLocalStorage();
    savePersistedData(createSeedData());
  });

  it("reset clears listings, service-orders and aggregator-statements (no stale survivors)", async () => {
    // Populate each store so its localStorage key is written with non-seed state.
    saveListing({
      catalogItemId: "af5-listing",
      requirements: [{ id: "r1", orderIndex: 0, title: "AF5 marker", type: "ShortText", choices: [] }],
      deliverables: [],
      executionSteps: [],
      terms: [],
      faqs: [],
    });
    const created = createOrder({
      catalogItemId: "af5-item",
      offeringName: "AF5 Order",
      partnerId: "af5-partner",
      tenantId: "af5-tenant",
      participationMode: "Principal",
      sellOrFee: 100,
      actor: "af5",
      at: new Date().toISOString(),
    });
    saveOrder(created.order!);
    importAggregatorStatement({
      id: "af5-stmt",
      aggregatorName: "AF5 Aggregator",
      periodLabel: "2026-06",
      currency: "SAR",
      lines: [],
      status: "Imported",
      contentHash: "af5-hash",
      importedAt: new Date().toISOString(),
      variances: [],
    } as never);

    for (const key of STORE_KEYS) {
      expect(localStorage.getItem(key), `${key} should be populated before reset`).not.toBeNull();
    }

    await new MockPortalDataSource().resetDemoData();

    // Every store's key is cleared (reset re-seeds from defaults on the next read).
    for (const key of STORE_KEYS) {
      expect(localStorage.getItem(key), `${key} must be cleared by reset`).toBeNull();
    }

    // The AF5 marker rows are gone (not merely coexisting with defaults).
    expect(listOrdersForTenant("af5-tenant").some((o) => o.id === created.order!.id)).toBe(false);
    expect(listAggregatorStatements().some((s) => s.id === "af5-stmt")).toBe(false);
  });
});
