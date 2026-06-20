import type { PortalData } from "./types";
import { createSeedData } from "./fixtures";

const STORAGE_KEY = "zahy-portal-demo-v1";

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
  if (migrated) savePersistedData(persisted);
  return persisted;
}

export function resetToSeedData(): PortalData {
  clearPersistedData();
  const seed = createSeedData();
  savePersistedData(seed);
  return seed;
}
