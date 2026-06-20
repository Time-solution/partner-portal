import { describe, expect, it } from "vitest";
import { buildChartAnalytics } from "./chartAnalytics";
import { mockPortalData } from "@/lib/data/fixtures";

describe("buildChartAnalytics", () => {
  it("computes KPIs from store data", () => {
    const a = buildChartAnalytics(mockPortalData);
    expect(a.kpis.activePartners).toBeGreaterThan(0);
    expect(a.kpis.reflectedOrders).toBe(mockPortalData.reflectedOrders.length);
    expect(a.settlementCaseCount).toBeGreaterThan(0);
  });

  it("maps partners to business modules for transaction chart", () => {
    const a = buildChartAnalytics(mockPortalData);
    const commerce = a.transactionsByModule.find((m) => m.moduleId === "commerce");
    const fnb = a.transactionsByModule.find((m) => m.moduleId === "fnb");
    expect(commerce?.volume).toBeGreaterThan(0);
    expect(fnb?.volume).toBeGreaterThan(0);
  });

  it("tracks pending activations separately from active", () => {
    const a = buildChartAnalytics(mockPortalData);
    const act = a.pendingVsDone.find((r) => r.categoryKey === "chartCategory_activations");
    expect(act?.pending).toBeGreaterThan(0);
    expect(act?.done).toBeGreaterThan(0);
  });

  it("builds settlement trend points from case dates", () => {
    const a = buildChartAnalytics(mockPortalData);
    expect(a.settlementTrend.length).toBeGreaterThan(1);
  });
});
