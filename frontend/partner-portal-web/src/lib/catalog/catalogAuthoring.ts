import type { OfferingKind, PartnerType } from "@/lib/data/types";

/** Service/subscription offerings — partner self-service tiers. */
export function isSelfServiceOfferingKind(kind: OfferingKind): boolean {
  return kind === "ServiceOneOff" || kind === "ServiceSubscription";
}

export function isAdminManagedOfferingKind(kind: OfferingKind): boolean {
  return !isSelfServiceOfferingKind(kind);
}

export function isSelfServicePartnerType(type: PartnerType): boolean {
  return type === "Service";
}

export function isAdminManagedPartnerType(type: PartnerType): boolean {
  return !isSelfServicePartnerType(type);
}

export type CatalogAuthoringMode = "self-service" | "admin-managed-readonly" | "admin-managed-write";

/**
 * Resolve whether the viewer can author catalog items for a partner.
 * Admin (managed write) can author any partner; service partners self-author only.
 */
export function resolveCatalogAuthoringMode(input: {
  partnerType: PartnerType;
  canAuthorSelf: boolean;
  canAuthorManaged: boolean;
}): CatalogAuthoringMode {
  if (input.canAuthorManaged) {
    return "admin-managed-write";
  }

  if (input.canAuthorSelf && isSelfServicePartnerType(input.partnerType)) {
    return "self-service";
  }

  return "admin-managed-readonly";
}

/**
 * Phase 6b — may the current viewer author the OFFERING/PACKAGE presentation fields
 * (MerchantBenefit, OfferingSummary, PackageExplanation)? Follows the SAME authoring-by-type
 * rule as catalog items: a service partner self-authors; delivery/3PL is admin-managed
 * (read-only for the partner). NOTE: PartnerBrief is NOT gated here — it is company
 * self-description, editable by every partner type.
 */
export function canAuthorOfferingPresentation(mode: CatalogAuthoringMode): boolean {
  return mode === "self-service" || mode === "admin-managed-write";
}

/** Mock UI-only tier grouping key (Option A — separate items, grouped in UI). */
export function tierGroupKey(code: string): string {
  const dash = code.indexOf("-");
  return dash > 0 ? code.slice(0, dash) : code;
}

export interface CatalogTierGroup<T extends { code: string; name: string }> {
  groupKey: string;
  label: string;
  items: T[];
}

export function groupCatalogTiers<T extends { code: string; name: string }>(items: T[]): CatalogTierGroup<T>[] {
  const map = new Map<string, T[]>();
  for (const item of items) {
    const key = tierGroupKey(item.code);
    const list = map.get(key) ?? [];
    list.push(item);
    map.set(key, list);
  }

  return [...map.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([groupKey, groupItems]) => ({
      groupKey,
      label: groupKey,
      items: [...groupItems].sort((a, b) => a.code.localeCompare(b.code)),
    }));
}
