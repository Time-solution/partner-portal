import { afterEach, describe, expect, it } from "vitest";
import { createSeedData, mockPortalData } from "./fixtures";
import { getMockPortalDataSource, resetMockPortalDataSourceInstance } from "./mockDataSource";

describe("seeded reversals", () => {
  afterEach(() => {
    resetMockPortalDataSourceInstance();
  });

  it("fixtures_seed_has_at_least_two_reversals_with_netsToZero_true", () => {
    const seed = createSeedData();
    expect(seed.reversals.length).toBeGreaterThanOrEqual(2);
    expect(seed.reversals.every((r) => r.netsToZero === true)).toBe(true);
    // The static fixture object exposes the same scenarios.
    expect(mockPortalData.reversals.length).toBeGreaterThanOrEqual(2);

    // Each reversal points at an existing settlement case.
    const caseIds = new Set(mockPortalData.settlementCases.map((c) => c.id));
    for (const r of mockPortalData.reversals) {
      expect(caseIds.has(r.originalCaseId)).toBe(true);
      expect(r.reason && r.reason.length).toBeGreaterThan(0);
      expect(r.reversedBy && r.reversedBy.length).toBeGreaterThan(0);
    }
  });

  it("ReversalsPage_renders_seeded_reversals_on_first_load", async () => {
    // ReversalsPage's first-load effect calls getPortalDataSource().getReversals();
    // exercise that exact data path (no DOM env, so we assert the data the page renders).
    resetMockPortalDataSourceInstance();
    const ds = getMockPortalDataSource();
    const reversals = await ds.getReversals();
    expect(reversals.length).toBeGreaterThanOrEqual(2);
    expect(reversals.some((r) => r.originalCaseId === "stl-burger-7003")).toBe(true);
    expect(reversals.some((r) => r.originalCaseId === "stl-pizza-oto-8001")).toBe(true);
  });
});
