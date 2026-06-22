import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  normalizeUsagePackageInput,
  scopeUsagePackage,
  type UsagePackage,
} from "./usagePackage";
import { resolveCatalogAuthoringMode as resolveAuthoring } from "@/lib/catalog/catalogAuthoring";

const WHATSAPP = "22222222-2222-2222-2222-222222222004";

function resale(): UsagePackage {
  return {
    id: "p1",
    partnerId: WHATSAPP,
    name: "Resale 10k",
    unitLabel: "messages",
    mode: "Resale",
    currency: "SAR",
    includedQuantity: 10000,
    baseBuyAmount: 200,
    baseSellAmount: 300,
    overageBuyAmount: 0.02,
    overageSellAmount: 0.05,
    payer: "Merchant",
    status: "Published",
  };
}

function subscription(): UsagePackage {
  return {
    id: "p2",
    partnerId: WHATSAPP,
    name: "Monthly",
    unitLabel: "messages",
    mode: "Subscription",
    currency: "SAR",
    includedQuantity: 5000,
    baseBuyAmount: 0,
    baseSellAmount: 149,
    overageBuyAmount: 0,
    overageSellAmount: 0.03,
    payer: "Partner",
    status: "Published",
  };
}

describe("usage package scoped visibility (reuses catalog buy/sell/margin rule)", () => {
  it("admin sees buy, sell AND margin for resale", () => {
    const s = scopeUsagePackage(resale(), "admin");
    expect(s.base.buy?.amount).toBe(200);
    expect(s.base.sell?.amount).toBe(300);
    expect(s.base.margin?.amount).toBe(100);
    expect(s.overage.margin?.amount).toBe(0.03);
  });

  it("partner sees BUY only — sell + margin are STRUCTURALLY ABSENT for resale", () => {
    const s = scopeUsagePackage(resale(), "partner");
    expect(s.base.buy?.amount).toBe(200);
    expect("sell" in s.base).toBe(false);
    expect("margin" in s.base).toBe(false);
    expect("sell" in s.overage).toBe(false);
    expect("margin" in s.overage).toBe(false);
  });

  it("merchant sees SELL only — buy + margin absent for resale", () => {
    const s = scopeUsagePackage(resale(), "merchant");
    expect(s.base.sell?.amount).toBe(300);
    expect("buy" in s.base).toBe(false);
    expect("margin" in s.base).toBe(false);
  });

  it("subscription shows fee + payer, never buy/sell/margin", () => {
    const s = scopeUsagePackage(subscription(), "partner");
    expect(s.base.fee?.amount).toBe(149);
    expect(s.overage.fee?.amount).toBe(0.03);
    expect(s.payer).toBe("Partner");
    expect("buy" in s.base).toBe(false);
    expect("sell" in s.base).toBe(false);
    expect("margin" in s.base).toBe(false);
  });
});

describe("usage package input normalization", () => {
  it("subscription forces the buy side to 0 (no partner payout)", () => {
    const norm = normalizeUsagePackageInput({
      partnerId: WHATSAPP,
      name: "Sub",
      mode: "Subscription",
      includedQuantity: 0,
      baseBuyAmount: 999,
      baseSellAmount: 149,
      overageBuyAmount: 999,
      overageSellAmount: 0.03,
      payer: "Merchant",
    });
    expect(norm.baseBuyAmount).toBe(0);
    expect(norm.overageBuyAmount).toBe(0);
    expect(norm.baseSellAmount).toBe(149);
  });
});

describe("authoring-by-type (reuses catalog rule): service self, delivery admin-managed", () => {
  it("service partner with AuthorSelf may self-publish", () => {
    expect(
      resolveAuthoring({ partnerType: "Service", canAuthorSelf: true, canAuthorManaged: false }),
    ).toBe("self-service");
  });

  it("carrier/delivery partner cannot self-author — read-only without admin", () => {
    expect(
      resolveAuthoring({ partnerType: "Carrier", canAuthorSelf: true, canAuthorManaged: false }),
    ).toBe("admin-managed-readonly");
  });

  it("admin manages-all any partner type", () => {
    expect(
      resolveAuthoring({ partnerType: "Carrier", canAuthorSelf: false, canAuthorManaged: true }),
    ).toBe("admin-managed-write");
  });
});

describe("usage package store — CRUD, both modes, multiple per partner (localStorage mock)", () => {
  beforeEach(() => {
    const mem: Record<string, string> = {};
    vi.stubGlobal("localStorage", {
      getItem: (k: string) => mem[k] ?? null,
      setItem: (k: string, v: string) => void (mem[k] = v),
      removeItem: (k: string) => void delete mem[k],
      clear: () => void Object.keys(mem).forEach((k) => delete mem[k]),
      key: () => null,
      length: 0,
    });
  });

  it("seeds demo packages for the service partner (both modes present)", async () => {
    const { listUsagePackages } = await import("./usagePackageStore");
    const all = listUsagePackages();
    expect(all.some((p) => p.mode === "Resale")).toBe(true);
    expect(all.some((p) => p.mode === "Subscription")).toBe(true);
  });

  it("creates multiple packages per partner and lists scoped by partner", async () => {
    const { createUsagePackage, listUsagePackages } = await import("./usagePackageStore");
    const before = listUsagePackages(WHATSAPP).length;
    createUsagePackage({
      partnerId: WHATSAPP,
      name: "New resale",
      mode: "Resale",
      includedQuantity: 1000,
      baseBuyAmount: 50,
      baseSellAmount: 80,
      overageBuyAmount: 0.01,
      overageSellAmount: 0.02,
    });
    createUsagePackage({
      partnerId: WHATSAPP,
      name: "New sub",
      mode: "Subscription",
      includedQuantity: 100,
      baseBuyAmount: 0,
      baseSellAmount: 99,
      overageBuyAmount: 0,
      overageSellAmount: 0.05,
      payer: "Merchant",
    });
    expect(listUsagePackages(WHATSAPP).length).toBe(before + 2);
  });

  it("publish then archive; archived cannot be published", async () => {
    const { createUsagePackage, publishUsagePackage, archiveUsagePackage } = await import("./usagePackageStore");
    const pkg = createUsagePackage({
      partnerId: WHATSAPP,
      name: "Lifecycle",
      mode: "Resale",
      includedQuantity: 0,
      baseBuyAmount: 0,
      baseSellAmount: 0,
      overageBuyAmount: 0.01,
      overageSellAmount: 0.02,
    });
    expect(pkg.status).toBe("Draft");
    expect(publishUsagePackage(pkg.id).status).toBe("Published");
    expect(archiveUsagePackage(pkg.id).status).toBe("Archived");
    expect(() => publishUsagePackage(pkg.id)).toThrow();
  });

  it("rejects negative amounts", async () => {
    const { createUsagePackage } = await import("./usagePackageStore");
    expect(() =>
      createUsagePackage({
        partnerId: WHATSAPP,
        name: "Bad",
        mode: "Resale",
        includedQuantity: 0,
        baseBuyAmount: -1,
        baseSellAmount: 0,
        overageBuyAmount: 0,
        overageSellAmount: 0,
      }),
    ).toThrow();
  });
});
