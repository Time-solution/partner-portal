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

export function merchantListedSellPrice(item: PartnerCatalogItem): Money {
  const amount = MOCK_LISTED_SELL_BY_CATALOG_ID[item.id] ?? 0;
  return { amount, currency: item.partnerCost.currency, vatInclusive: item.partnerCost.vatInclusive };
}

export function buildMerchantActivationIdempotencyKey(tenantId: string, catalogItemId: string): string {
  return `activation:${tenantId}:${catalogItemId}`;
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
