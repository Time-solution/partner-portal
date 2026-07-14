import { isSelfServicePartnerType } from "@/lib/catalog/catalogAuthoring";
import type { Money, Partner, PartnerCatalogItem } from "@/lib/data/types";
import {
  moduleLabelKey,
  resolvePartnerModule,
  type PartnerBusinessModuleId,
} from "@/lib/rbac/partnerModules";

/** Browse category order — consignment stays parked (excluded). */
export const MERCHANT_BROWSE_CATEGORY_ORDER: readonly PartnerBusinessModuleId[] = [
  "delivery-service",
  "commerce",
  "fnb",
  "subscriptions",
  "marketplace",
] as const;

export interface MerchantBrowsePartnerEntry {
  partner: Partner;
  moduleId: PartnerBusinessModuleId;
  /** Active platform catalog rows for this partner (GetAvailableOfferings shape). */
  offerings: PartnerCatalogItem[];
  /** Service/subscription partners — merchant picks a tier. */
  hasTierMenu: boolean;
}

/**
 * The merchant-scoped offering projection — FE mirror of the backend's single audited mapper
 * (PartnerCatalogOfferingVisibility → MerchantPartnerOfferingReadDto). Fresh object literal:
 * partnerCost / buy / margin are STRUCTURALLY ABSENT (no key exists), the only money field is
 * the merchant-named `price`. Merchant-facing components must receive THIS shape, never the raw
 * PartnerCatalogItem.
 */
export interface MerchantOfferingView {
  id: string;
  partnerId: string;
  code: string;
  name: string;
  description?: string;
  merchantBenefit?: string;
  offeringKind: PartnerCatalogItem["offeringKind"];
  participationMode: PartnerCatalogItem["participationMode"];
  /** The resolved sell — what activation will charge (merchantOfferingPrice). */
  price: Money;
}

export function merchantOfferingView(item: PartnerCatalogItem): MerchantOfferingView {
  return {
    id: item.id,
    partnerId: item.partnerId,
    code: item.code,
    name: item.name,
    description: item.description,
    merchantBenefit: item.merchantBenefit,
    offeringKind: item.offeringKind,
    participationMode: item.participationMode,
    price: merchantOfferingPrice(item),
  };
}

/** A browse entry with offerings already projected through the merchant scope. */
export interface MerchantBrowsePartnerViewEntry {
  partner: Partner;
  moduleId: PartnerBusinessModuleId;
  offerings: MerchantOfferingView[];
  hasTierMenu: boolean;
}

export function toMerchantViewEntry(entry: MerchantBrowsePartnerEntry): MerchantBrowsePartnerViewEntry {
  return {
    partner: entry.partner,
    moduleId: entry.moduleId,
    offerings: entry.offerings.map(merchantOfferingView),
    hasTierMenu: entry.hasTierMenu,
  };
}

export interface MerchantBrowseCategoryGroup {
  moduleId: PartnerBusinessModuleId;
  partners: MerchantBrowsePartnerEntry[];
}

/**
 * Mock listed sell prices for merchant browse — keyed by catalog item id.
 * Single source for merchant-facing sell leg (not partner buy).
 */
const MOCK_LISTED_SELL_BY_CATALOG_ID: Record<string, number> = {
  "a1000001-0001-4000-8000-000000000001": 13,
  "a1000002-0002-4000-8000-000000000002": 35,
  "a1000003-0003-4000-8000-000000000003": 59,
  "a1000003b-0003-4000-8000-00000000003b": 100,
  "a1000003c-0003-4000-8000-00000000003c": 169,
  "a1000008-0008-4000-8000-000000000008": 69,
  "a1000008b-0008-4000-8000-00000000008b": 119,
  "a1000008c-0008-4000-8000-00000000008c": 199,
  "a1000006-0006-4000-8000-000000000006": 0,
  "a1000007-0007-4000-8000-000000000007": 13,
};

/**
 * The merchant-named Price (mirrors backend MerchantPartnerOfferingReadDto.Price): the resolved
 * SELL the merchant sees on browse and gets charged on activation — never the partner buy leg.
 * Items without a curated listed sell fall back to the backend's ResolveResalePrice default
 * (the cost VALUE re-used as the default sell), so browse price == what activation charges.
 */
export function merchantOfferingPrice(item: PartnerCatalogItem): Money {
  const amount = MOCK_LISTED_SELL_BY_CATALOG_ID[item.id] ?? item.partnerCost.amount;
  return { amount, currency: item.partnerCost.currency, vatInclusive: item.partnerCost.vatInclusive };
}

/**
 * Mirrors backend MerchantActivation.BuildIdempotencyKey: sequence-suffixed so a merchant can
 * re-activate after ending (ratified) — each ended cycle increments the suffix; the new cycle is a
 * NEW activation (new window/snapshot), ended rows are never mutated.
 */
export function buildMerchantActivationIdempotencyKey(
  tenantId: string,
  catalogItemId: string,
  priorEndedCount: number,
): string {
  return `activation:${tenantId}:${catalogItemId}:${priorEndedCount}`;
}

export function isPartnerBrowsable(partner: Partner, activeOfferings: PartnerCatalogItem[]): boolean {
  if (partner.status !== "Active") return false;
  if (partner.parkedModule === "consignment") return false;
  if (resolvePartnerModule(partner) === "consignment") return false;
  return activeOfferings.length > 0;
}

export function partnerHasTierMenu(partner: Partner, offerings: PartnerCatalogItem[]): boolean {
  return isSelfServicePartnerType(partner.type) && offerings.length > 0;
}

/** All platform-active partners with ≥1 active offering, grouped by business module. */
export function buildMerchantBrowseGroups(
  partners: Partner[],
  catalogItems: PartnerCatalogItem[],
): MerchantBrowseCategoryGroup[] {
  const activeItems = catalogItems.filter((c) => c.status === "Active");
  const byModule = new Map<PartnerBusinessModuleId, MerchantBrowsePartnerEntry[]>();

  for (const partner of partners) {
    const offerings = activeItems
      .filter((c) => c.partnerId === partner.id)
      .sort((a, b) => a.code.localeCompare(b.code));

    if (!isPartnerBrowsable(partner, offerings)) continue;

    const moduleId = resolvePartnerModule(partner);
    if (moduleId === "consignment") continue;

    const entry: MerchantBrowsePartnerEntry = {
      partner,
      moduleId,
      offerings,
      hasTierMenu: partnerHasTierMenu(partner, offerings),
    };

    const list = byModule.get(moduleId) ?? [];
    list.push(entry);
    byModule.set(moduleId, list);
  }

  return MERCHANT_BROWSE_CATEGORY_ORDER.filter((moduleId) => byModule.has(moduleId)).map((moduleId) => ({
    moduleId,
    partners: (byModule.get(moduleId) ?? []).sort((a, b) =>
      (a.partner.tradeName ?? a.partner.legalName).localeCompare(
        b.partner.tradeName ?? b.partner.legalName,
      ),
    ),
  }));
}

export function browseCategoryLabelKey(moduleId: PartnerBusinessModuleId): string {
  return moduleLabelKey(moduleId);
}
