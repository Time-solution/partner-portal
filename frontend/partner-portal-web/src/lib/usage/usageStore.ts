import {
  DEFAULT_UNIT_LABEL,
  DEFAULT_USAGE_SOURCE,
  type UsageRecord,
  type UsageRecordInput,
} from "./usageRecord";

/**
 * U1 — sibling localStorage store for usage metering (mock), mirroring the `bankRegistryStore` pattern.
 * Kept SEPARATE from the main PortalData: usage is host-level metering data, recorded incrementally and
 * summed per period (see `usageReports`). APPEND/ACCUMULATE — never overwrites; PURE DATA, never bills.
 */

// Bumped v1 -> v2 to force a clean re-seed of the COMPLETE usage demo dataset.
const STORAGE_KEY = "zahy-usage-records-v2";

// Demo IDs — mirror the fixtures (WhatsApp Co / W there + their active merchants).
const PARTNER_WHATSAPP = "22222222-2222-2222-2222-222222222004";
const PARTNER_WTHERE = "22222222-2222-2222-2222-222222222008";
const TENANT_PIZZA = "11111111-1111-1111-1111-111111111003";
const TENANT_COFFEE = "11111111-1111-1111-1111-111111111002";
const TENANT_BURGER = "11111111-1111-1111-1111-111111111001";

/**
 * Seeded demo usage so EVERY billing case is testable across 2026-06 AND 2026-07. Incremental rows
 * (multiple per partner+merchant+period) demonstrate the accumulate model. Pizza House's 6,200 in
 * 2026-06 stays as 4,000 + 2,200. Cases covered: UNDER included, OVER (single flat tier), OVER
 * CROSSING tiers (graduated), PURE PER-USE, and a MID-CYCLE case aligned to the prorated selection.
 */
function seedDefaults(): UsageRecord[] {
  return [
    // ── 2026-06 ─────────────────────────────────────────────────────────────────
    // Pizza @ WhatsApp tiered (included 5,000): 6,200 -> OVER, within the first tier (single-tier overage).
    { id: "use-pizza-wa-06a", partnerId: PARTNER_WHATSAPP, tenantId: TENANT_PIZZA, period: "2026-06", unitLabel: "messages", quantity: 4000, source: "seed" },
    { id: "use-pizza-wa-06b", partnerId: PARTNER_WHATSAPP, tenantId: TENANT_PIZZA, period: "2026-06", unitLabel: "messages", quantity: 2200, source: "seed" },
    // Coffee @ WhatsApp sub flat (included 5,000): 3,100 -> UNDER (base/fee only, no overage).
    { id: "use-coffee-wa-06", partnerId: PARTNER_WHATSAPP, tenantId: TENANT_COFFEE, period: "2026-06", unitLabel: "messages", quantity: 3100, source: "seed" },
    // Burger @ W there resale flat (included 4,000): 4,800 -> OVER, single flat tier (+800 @0.05).
    { id: "use-burger-wt-06", partnerId: PARTNER_WTHERE, tenantId: TENANT_BURGER, period: "2026-06", unitLabel: "messages", quantity: 4800, source: "seed" },
    // Coffee @ W there pure per-use (included 0): 2,000 -> PURE PER-USE (every unit billed).
    { id: "use-coffee-wt-06", partnerId: PARTNER_WTHERE, tenantId: TENANT_COFFEE, period: "2026-06", unitLabel: "messages", quantity: 2000, source: "seed" },

    // ── 2026-07 ─────────────────────────────────────────────────────────────────
    // Pizza @ WhatsApp tiered: 16,200 (incremental 10,000 + 6,200) -> overage 11,200 CROSSES 10,000
    // boundary -> 10,000×0.05 + 1,200×0.04 = 548 sell (graduated calc).
    { id: "use-pizza-wa-07a", partnerId: PARTNER_WHATSAPP, tenantId: TENANT_PIZZA, period: "2026-07", unitLabel: "messages", quantity: 10000, source: "seed" },
    { id: "use-pizza-wa-07b", partnerId: PARTNER_WHATSAPP, tenantId: TENANT_PIZZA, period: "2026-07", unitLabel: "messages", quantity: 6200, source: "seed" },
    // Coffee @ WhatsApp sub flat: 8,000 -> OVER, single flat tier (+3,000 @0.03).
    { id: "use-coffee-wa-07", partnerId: PARTNER_WHATSAPP, tenantId: TENANT_COFFEE, period: "2026-07", unitLabel: "messages", quantity: 8000, source: "seed" },
    // Burger @ W there resale flat: 3,500 -> UNDER (base only).
    { id: "use-burger-wt-07", partnerId: PARTNER_WTHERE, tenantId: TENANT_BURGER, period: "2026-07", unitLabel: "messages", quantity: 3500, source: "seed" },
    // Burger @ WhatsApp sub flat: 7,000 -> aligned with the MID-CYCLE selection (base day-prorated, overage +2,000).
    { id: "use-burger-wa-07", partnerId: PARTNER_WHATSAPP, tenantId: TENANT_BURGER, period: "2026-07", unitLabel: "messages", quantity: 7000, source: "seed" },
    // Coffee @ W there pure per-use: 2,600 -> PURE PER-USE continued.
    { id: "use-coffee-wt-07", partnerId: PARTNER_WTHERE, tenantId: TENANT_COFFEE, period: "2026-07", unitLabel: "messages", quantity: 2600, source: "seed" },

    // Prior-period continuity (kept): Pizza @ WhatsApp in 2026-05.
    { id: "use-pizza-wa-may", partnerId: PARTNER_WHATSAPP, tenantId: TENANT_PIZZA, period: "2026-05", unitLabel: "messages", quantity: 5000, source: "seed" },
  ];
}

function loadList(): UsageRecord[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return seedDefaults();
    const parsed = JSON.parse(raw) as UsageRecord[];
    return Array.isArray(parsed) ? parsed : seedDefaults();
  } catch {
    return seedDefaults();
  }
}

function saveList(list: UsageRecord[]): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
}

export function listUsageRecords(): UsageRecord[] {
  return loadList().slice();
}

function uid(): string {
  return `use-${Math.random().toString(36).slice(2, 10)}`;
}

/** Append a usage record (incremental). Never overwrites — records accumulate per partner+merchant+period. */
export function recordUsage(input: UsageRecordInput): UsageRecord {
  if (input.quantity < 0) throw new Error("Usage quantity cannot be negative");
  const list = loadList();
  const record: UsageRecord = {
    id: uid(),
    partnerId: input.partnerId,
    tenantId: input.tenantId,
    period: input.period,
    unitLabel: (input.unitLabel || DEFAULT_UNIT_LABEL).trim(),
    quantity: input.quantity,
    source: (input.source || DEFAULT_USAGE_SOURCE).trim(),
  };
  saveList([...list, record]);
  return record;
}

export function resetUsageRecords(): void {
  localStorage.removeItem(STORAGE_KEY);
}
