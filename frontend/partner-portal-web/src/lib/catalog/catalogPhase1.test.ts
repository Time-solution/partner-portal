import { describe, expect, it } from "vitest";
import {
  forbiddenPriceKeys,
  projectCatalogPrice,
  type CatalogPriceViewer,
} from "./catalogPriceVisibility";

const sar = (amount: number) => ({ amount, currency: "SAR", vatInclusive: true });

const source = { buy: sar(10), sell: sar(13) };

function assertAbsent(viewer: CatalogPriceViewer) {
  const projected = projectCatalogPrice(source, viewer);
  for (const key of forbiddenPriceKeys(viewer)) {
    expect(projected).not.toHaveProperty(key);
  }
}

describe("projectCatalogPrice", () => {
  it("accountant sees buy, sell, and margin", () => {
    const p = projectCatalogPrice(source, "accountant");
    expect(p.buy?.amount).toBe(10);
    expect(p.sell?.amount).toBe(13);
    expect(p.margin?.amount).toBe(3);
  });

  it("partner render has NO sell or margin", () => {
    assertAbsent("partner");
    const p = projectCatalogPrice(source, "partner");
    expect(p.buy?.amount).toBe(10);
  });

  it("merchant render has NO buy or margin", () => {
    assertAbsent("merchant");
    const p = projectCatalogPrice(source, "merchant");
    expect(p.sell?.amount).toBe(13);
  });

  it("admin sees all legs", () => {
    const p = projectCatalogPrice(source, "admin");
    expect(p.buy).toBeDefined();
    expect(p.sell).toBeDefined();
    expect(p.margin?.amount).toBe(3);
  });
});

describe("groupCatalogTiers", () => {
  it("groups 3 tier items under one service", async () => {
    const { groupCatalogTiers } = await import("./catalogAuthoring");
    const groups = groupCatalogTiers([
      { code: "SVC-BASIC", name: "Basic" },
      { code: "SVC-PRO", name: "Pro" },
      { code: "SVC-ENT", name: "Enterprise" },
    ]);
    expect(groups).toHaveLength(1);
    expect(groups[0]?.items).toHaveLength(3);
  });
});

describe("resolveCatalogAuthoringMode", () => {
  it("delivery partner is read-only for partner finance", async () => {
    const { resolveCatalogAuthoringMode } = await import("./catalogAuthoring");
    expect(
      resolveCatalogAuthoringMode({
        partnerType: "Carrier",
        canAuthorSelf: true,
        canAuthorManaged: false,
      }),
    ).toBe("admin-managed-readonly");
  });

  it("service partner with AuthorSelf is self-service", async () => {
    const { resolveCatalogAuthoringMode } = await import("./catalogAuthoring");
    expect(
      resolveCatalogAuthoringMode({
        partnerType: "Service",
        canAuthorSelf: true,
        canAuthorManaged: false,
      }),
    ).toBe("self-service");
  });
});
