import { beforeEach, describe, expect, it, vi } from "vitest";
import { USAGE_FEED_WIRED, usageFeedAutoPush, type UsageRecord } from "./usageRecord";
import {
  partnerPeriodRollup,
  scopeUsage,
  usageForPeriod,
  type UsageViewer,
} from "./usageReports";

const WHATSAPP = "22222222-2222-2222-2222-222222222004";
const WTHERE = "22222222-2222-2222-2222-222222222008";
const PIZZA = "11111111-1111-1111-1111-111111111003";
const COFFEE = "11111111-1111-1111-1111-111111111002";
const BURGER = "11111111-1111-1111-1111-111111111001";

function rec(partnerId: string, tenantId: string, period: string, quantity: number): UsageRecord {
  return { id: Math.random().toString(36), partnerId, tenantId, period, unitLabel: "messages", quantity, source: "seed" };
}

function demo(): UsageRecord[] {
  return [
    rec(WHATSAPP, PIZZA, "2026-06", 4000),
    rec(WHATSAPP, PIZZA, "2026-06", 2200), // incremental — accumulates to 6,200
    rec(WHATSAPP, COFFEE, "2026-06", 3100),
    rec(WTHERE, BURGER, "2026-06", 4800),
    rec(WHATSAPP, PIZZA, "2026-05", 5000),
  ];
}

describe("usage read model — sums metered quantity per partner+merchant+period", () => {
  it("usageForPeriod accumulates incremental records into the period total", () => {
    const records = demo();
    expect(usageForPeriod(records, WHATSAPP, PIZZA, "2026-06")).toBe(6200);
    expect(usageForPeriod(records, WHATSAPP, COFFEE, "2026-06")).toBe(3100);
    expect(usageForPeriod(records, WTHERE, BURGER, "2026-06")).toBe(4800);
  });

  it("is keyed to partner + merchant + period (no leakage across keys)", () => {
    const records = demo();
    expect(usageForPeriod(records, WHATSAPP, PIZZA, "2026-05")).toBe(5000);
    expect(usageForPeriod(records, WTHERE, PIZZA, "2026-06")).toBe(0);
    expect(usageForPeriod(records, WHATSAPP, BURGER, "2026-06")).toBe(0);
  });

  it("accumulates further when more usage is appended", () => {
    const records = demo();
    records.push(rec(WHATSAPP, PIZZA, "2026-06", 800));
    expect(usageForPeriod(records, WHATSAPP, PIZZA, "2026-06")).toBe(7000);
  });

  it("partnerPeriodRollup breaks down per merchant and totals the partner", () => {
    const rollup = partnerPeriodRollup(demo(), WHATSAPP, "2026-06");
    expect(rollup.perMerchant).toHaveLength(2);
    expect(rollup.perMerchant.find((m) => m.tenantId === PIZZA)?.quantity).toBe(6200);
    expect(rollup.perMerchant.find((m) => m.tenantId === COFFEE)?.quantity).toBe(3100);
    expect(rollup.totalQuantity).toBe(9300); // May excluded
  });
});

describe("usage scope — RBAC row-level, no cross-entity leakage", () => {
  it("platform (accountant/admin) sees all usage", () => {
    expect(scopeUsage(demo(), { kind: "platform" })).toHaveLength(5);
  });

  it("a partner sees only their own merchants' usage", () => {
    const scoped = scopeUsage(demo(), { kind: "partner", partnerId: WHATSAPP });
    expect(scoped.every((r) => r.partnerId === WHATSAPP)).toBe(true);
    expect(scoped.some((r) => r.partnerId === WTHERE)).toBe(false);
  });

  it("a merchant sees only their own usage across partners", () => {
    const scoped = scopeUsage(demo(), { kind: "merchant", tenantId: PIZZA });
    expect(scoped.every((r) => r.tenantId === PIZZA)).toBe(true);
    expect(scoped.some((r) => r.tenantId === BURGER)).toBe(false);
  });

  it("scope composes with the rollup without leaking another partner's usage", () => {
    const viewer: UsageViewer = { kind: "partner", partnerId: WHATSAPP };
    const visible = scopeUsage(demo(), viewer);
    const rollup = partnerPeriodRollup(visible, WHATSAPP, "2026-06");
    expect(rollup.totalQuantity).toBe(9300);
  });
});

describe("usage auto-feed seam — present but NOT wired", () => {
  it("the wired flag is false", () => {
    expect(USAGE_FEED_WIRED).toBe(false);
  });

  it("the auto-push path throws (not implemented)", () => {
    expect(() => usageFeedAutoPush([{ partnerId: WHATSAPP, tenantId: PIZZA, period: "2026-06", quantity: 10 }])).toThrow();
  });
});

describe("usage store — seeded demo + append/accumulate (localStorage mock)", () => {
  beforeEach(() => {
    const mem: Record<string, string> = {};
    vi.stubGlobal("localStorage", {
      getItem: (k: string) => mem[k] ?? null,
      setItem: (k: string, v: string) => void (mem[k] = v),
      removeItem: (k: string) => void delete mem[k],
      clear: () => void Object.keys(mem).forEach((k) => delete mem[k]),
    });
  });

  it("seeds demo usage so Pizza's 6,200 messages are testable", async () => {
    const { listUsageRecords } = await import("./usageStore");
    const records = listUsageRecords();
    expect(usageForPeriod(records, WHATSAPP, PIZZA, "2026-06")).toBe(6200);
  });

  it("recordUsage appends incrementally and accumulates", async () => {
    const { listUsageRecords, recordUsage } = await import("./usageStore");
    recordUsage({ partnerId: WHATSAPP, tenantId: PIZZA, period: "2026-06", unitLabel: "messages", quantity: 800, source: "manual" });
    expect(usageForPeriod(listUsageRecords(), WHATSAPP, PIZZA, "2026-06")).toBe(7000);
  });

  it("rejects a negative quantity", async () => {
    const { recordUsage } = await import("./usageStore");
    expect(() => recordUsage({ partnerId: WHATSAPP, tenantId: PIZZA, period: "2026-06", quantity: -5 })).toThrow();
  });
});
