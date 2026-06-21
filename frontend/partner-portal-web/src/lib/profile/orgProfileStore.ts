import {
  DEFAULT_MERCHANT_PROFILES,
  DEFAULT_PARTNER_PROFILES,
  DEFAULT_PLATFORM_PROFILE,
  emptyOrgProfile,
  orgProfileScopeKey,
  type OrgProfile,
  type OrgProfileScope,
} from "./orgProfile";

const STORAGE_KEY = "zahy-org-profiles-v1";

type ProfileMap = Record<string, OrgProfile>;

function seedDefaults(): ProfileMap {
  const map: ProfileMap = {
    [orgProfileScopeKey({ kind: "platform", id: "zahy" })]: { ...DEFAULT_PLATFORM_PROFILE },
  };
  for (const [partnerId, profile] of Object.entries(DEFAULT_PARTNER_PROFILES)) {
    map[orgProfileScopeKey({ kind: "partner", id: partnerId })] = { ...profile };
  }
  for (const [tenantId, profile] of Object.entries(DEFAULT_MERCHANT_PROFILES)) {
    map[orgProfileScopeKey({ kind: "merchant", id: tenantId })] = { ...profile };
  }
  return map;
}

function loadMap(): ProfileMap {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return seedDefaults();
    const parsed = JSON.parse(raw) as ProfileMap;
    return { ...seedDefaults(), ...parsed };
  } catch {
    return seedDefaults();
  }
}

function saveMap(map: ProfileMap): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(map));
}

export function readOrgProfile(scope: OrgProfileScope): OrgProfile {
  const map = loadMap();
  const key = orgProfileScopeKey(scope);
  const stored = map[key];
  if (stored) {
    return {
      ...emptyOrgProfile(),
      ...stored,
      contacts: { ...emptyOrgProfile().contacts, ...stored.contacts },
    };
  }
  return emptyOrgProfile();
}

export function writeOrgProfile(scope: OrgProfileScope, profile: OrgProfile): OrgProfile {
  const map = loadMap();
  const key = orgProfileScopeKey(scope);
  const next: OrgProfile = {
    ...profile,
    contacts: { ...profile.contacts },
  };
  map[key] = next;
  saveMap(map);
  return { ...next, contacts: { ...next.contacts } };
}

export function resetOrgProfiles(): void {
  localStorage.removeItem(STORAGE_KEY);
}
