/**
 * U4 — sibling localStorage store for merchant package SELECTIONS (mock), mirroring the `usageStore` /
 * `usagePackageStore` pattern. Selecting a PUBLISHED package is an INSTANT self-service activation (no
 * approval). SELECTION/LINK ONLY — no billing computed, no journal posted (U3 reads the active window).
 */
import { listUsagePackages } from "./usagePackageStore";
import type {
  SelectUsagePackageInput,
  UsagePackageSelection,
} from "./usagePackageSelection";

// Bumped v1 -> v2 to force a clean re-seed of the COMPLETE selection demo dataset.
const STORAGE_KEY = "zahy-usage-package-selections-v2";

// Demo IDs (mirror the fixtures + usagePackageStore + usageStore seeds).
const PARTNER_WHATSAPP = "22222222-2222-2222-2222-222222222004";
const PARTNER_WTHERE = "22222222-2222-2222-2222-222222222008";
const TENANT_BURGER = "11111111-1111-1111-1111-111111111001";
const TENANT_COFFEE = "11111111-1111-1111-1111-111111111002";
const TENANT_PIZZA = "11111111-1111-1111-1111-111111111003";

/**
 * Seeded active selections so every merchant preview shows a real invoice breakdown out of the box,
 * exercising each billing case: tiered resale, flat subscription, flat resale, pure per-use, and one
 * MID-CYCLE activation (base day-proration). SELECTION/LINK ONLY — no billing computed, no journal posted.
 */
function seedDefaults(): UsagePackageSelection[] {
  return [
    // Pizza House on WhatsApp RESALE TIERED — active full period (exercises U5 graduated calc).
    {
      id: "sel-pizza-wa-tiered",
      partnerId: PARTNER_WHATSAPP,
      tenantId: TENANT_PIZZA,
      usagePackageId: "pkg-wa-resale-10k",
      merchantName: "Pizza House",
      status: "Active",
      activatedAt: "2026-06-01",
    },
    // Coffee Spot on WhatsApp SUBSCRIPTION FLAT — active full period.
    {
      id: "sel-coffee-wa-sub",
      partnerId: PARTNER_WHATSAPP,
      tenantId: TENANT_COFFEE,
      usagePackageId: "pkg-wa-sub-flat",
      merchantName: "Coffee Spot",
      status: "Active",
      activatedAt: "2026-06-01",
    },
    // Burger Co on W there RESALE FLAT — active full period.
    {
      id: "sel-burger-wt-resale",
      partnerId: PARTNER_WTHERE,
      tenantId: TENANT_BURGER,
      usagePackageId: "pkg-wt-resale-flat",
      merchantName: "Burger Co",
      status: "Active",
      activatedAt: "2026-06-01",
    },
    // Burger Co on WhatsApp SUBSCRIPTION FLAT — MID-CYCLE activation (2026-07-16) -> base day-proration.
    {
      id: "sel-burger-wa-sub-mid",
      partnerId: PARTNER_WHATSAPP,
      tenantId: TENANT_BURGER,
      usagePackageId: "pkg-wa-sub-flat",
      merchantName: "Burger Co",
      status: "Active",
      activatedAt: "2026-07-16",
    },
    // Coffee Spot on W there PURE PER-USE — active full period.
    {
      id: "sel-coffee-wt-payg",
      partnerId: PARTNER_WTHERE,
      tenantId: TENANT_COFFEE,
      usagePackageId: "pkg-wt-payg",
      merchantName: "Coffee Spot",
      status: "Active",
      activatedAt: "2026-06-01",
    },
  ];
}

function loadList(): UsagePackageSelection[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return seedDefaults();
    const parsed = JSON.parse(raw) as UsagePackageSelection[];
    return Array.isArray(parsed) ? parsed : seedDefaults();
  } catch {
    return seedDefaults();
  }
}

function saveList(list: UsagePackageSelection[]): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
}

function uid(): string {
  return `sel-${Math.random().toString(36).slice(2, 10)}`;
}

/** Selections for a merchant (most-recent first), or all when no tenant given. */
export function listUsagePackageSelections(tenantId?: string): UsagePackageSelection[] {
  const all = loadList();
  return (tenantId ? all.filter((s) => s.tenantId === tenantId) : all)
    .slice()
    .sort((a, b) => b.activatedAt.localeCompare(a.activatedAt));
}

/** The merchant's current ACTIVE selection for a partner (the package they're on), if any. */
export function activeSelectionFor(tenantId: string, partnerId: string): UsagePackageSelection | undefined {
  return loadList().find(
    (s) => s.tenantId === tenantId && s.partnerId === partnerId && s.status === "Active",
  );
}

/**
 * Instantly select (activate) a PUBLISHED package. Validates the package exists and is Published — a
 * draft/archived package is not selectable (mirrors the backend guard). No approval step.
 */
export function selectUsagePackage(input: SelectUsagePackageInput): UsagePackageSelection {
  const pkg = listUsagePackages().find((p) => p.id === input.usagePackageId);
  if (!pkg) throw new Error("Usage package not found");
  if (pkg.status !== "Published") throw new Error("Only published packages can be selected");
  if (pkg.partnerId !== input.partnerId) throw new Error("Package does not belong to the partner");

  const selection: UsagePackageSelection = {
    id: uid(),
    partnerId: input.partnerId,
    tenantId: input.tenantId,
    usagePackageId: input.usagePackageId,
    merchantName: input.merchantName,
    status: "Active",
    activatedAt: input.activatedAt ?? new Date().toISOString().slice(0, 10),
  };
  saveList([...loadList(), selection]);
  return selection;
}

/** Deactivate a selection mid-cycle (base prorated by days, usage counted to deactivation in U3). */
export function endUsagePackageSelection(id: string, endedAt?: string): UsagePackageSelection {
  const list = loadList();
  const idx = list.findIndex((s) => s.id === id);
  if (idx < 0) throw new Error("Selection not found");
  if (list[idx].status === "Ended") throw new Error("Selection already ended");
  const updated: UsagePackageSelection = {
    ...list[idx],
    status: "Ended",
    endedAt: endedAt ?? new Date().toISOString().slice(0, 10),
  };
  const next = list.slice();
  next[idx] = updated;
  saveList(next);
  return updated;
}

export function resetUsagePackageSelections(): void {
  localStorage.removeItem(STORAGE_KEY);
}
