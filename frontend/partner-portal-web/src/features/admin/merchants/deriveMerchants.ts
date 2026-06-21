import type {
  MerchantActivation,
  ParticipationMode,
  PortalData,
} from "@/lib/data/types";

export interface MerchantConnectedPartner {
  partnerId: string;
  partnerName: string;
  participationMode: ParticipationMode;
}

export interface MerchantSummary {
  /** Backend MerchantActivation.TenantId — raw key (use formatTenantRef for display). */
  tenantId: string;
  /** Matching merchant Org id, when one exists. */
  orgId?: string;
  name: string;
  /** Org status when a merchant org exists, else derived from activations. */
  status: string;
  activeActivationCount: number;
  totalActivationCount: number;
  connectedPartners: MerchantConnectedPartner[];
  activations: MerchantActivation[];
}

function activationSortKey(a: MerchantActivation): string {
  return a.activatedAt ?? a.endedAt ?? a.id;
}

/**
 * Derive the platform-wide merchant list from the (already org-scoped) portal data.
 * No new backend fields: a "merchant" is a distinct activation TenantId; "connected partners"
 * are the partners that merchant has activations with; participation mode comes from the partner.
 */
export function deriveMerchants(data: PortalData): MerchantSummary[] {
  const byTenant = new Map<string, MerchantActivation[]>();
  for (const a of data.activations) {
    const list = byTenant.get(a.tenantId) ?? [];
    list.push(a);
    byTenant.set(a.tenantId, list);
  }

  const partnersById = new Map(data.partners.map((p) => [p.id, p]));
  const merchantOrgByTenant = new Map(
    data.orgs
      .filter((o) => o.level === "Merchant" && o.tenantId)
      .map((o) => [o.tenantId as string, o]),
  );

  const merchants: MerchantSummary[] = [];
  for (const [tenantId, activations] of byTenant) {
    const org = merchantOrgByTenant.get(tenantId);
    const name = org?.name ?? activations[0]?.merchantName ?? tenantId;

    const partnerMap = new Map<string, MerchantConnectedPartner>();
    for (const a of activations) {
      if (partnerMap.has(a.partnerId)) continue;
      const p = partnersById.get(a.partnerId);
      partnerMap.set(a.partnerId, {
        partnerId: a.partnerId,
        partnerName: p?.tradeName ?? p?.legalName ?? a.partnerId,
        participationMode: p?.participationMode ?? "Principal",
      });
    }

    merchants.push({
      tenantId,
      orgId: org?.id,
      name,
      status: org?.status ?? "Active",
      activeActivationCount: activations.filter((a) => a.status === "Active").length,
      totalActivationCount: activations.length,
      connectedPartners: [...partnerMap.values()].sort((x, y) =>
        x.partnerName.localeCompare(y.partnerName),
      ),
      activations: [...activations].sort((x, y) =>
        activationSortKey(y).localeCompare(activationSortKey(x)),
      ),
    });
  }

  return merchants.sort((a, b) => a.name.localeCompare(b.name));
}

export function findMerchant(data: PortalData, tenantId: string): MerchantSummary | undefined {
  return deriveMerchants(data).find((m) => m.tenantId === tenantId);
}
