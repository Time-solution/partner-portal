import type { PortalData } from "./types";
import { createSeedData } from "./fixtures";

// Bumped to v7 for the four-way order split (Merchant + Delivery + ZahyMargin + NetVAT to ZATCA).
const STORAGE_KEY = "zahy-portal-demo-v8";

export function loadPersistedData(): PortalData | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as PortalData;
  } catch {
    return null;
  }
}

export function savePersistedData(data: PortalData): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
}

export function clearPersistedData(): void {
  localStorage.removeItem(STORAGE_KEY);
}

export function loadOrSeedData(): PortalData {
  const persisted = loadPersistedData();
  if (!persisted) return createSeedData();
  let migrated = false;
  if (!persisted.users) {
    persisted.users = createSeedData().users;
    migrated = true;
  }
  // Multi-org migration for demos persisted before orgs/orgUsers existed.
  if (!persisted.orgs || !persisted.orgUsers) {
    const seed = createSeedData();
    persisted.orgs = persisted.orgs ?? seed.orgs;
    persisted.orgUsers = persisted.orgUsers ?? seed.orgUsers;
    migrated = true;
  }
  // Money-cycle migration — receipts collection added in v3.
  if (!persisted.receipts) {
    persisted.receipts = createSeedData().receipts;
    migrated = true;
  }
  if (migrated) savePersistedData(persisted);
  return persisted;
}

export function resetToSeedData(): PortalData {
  clearPersistedData();
  const seed = createSeedData();
  savePersistedData(seed);
  return seed;
}
