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
  if (!persisted.users) {
    persisted.users = createSeedData().users;
    savePersistedData(persisted);
  }
  return persisted;
}

export function resetToSeedData(): PortalData {
  clearPersistedData();
  const seed = createSeedData();
  savePersistedData(seed);
  return seed;
}
