import { useEffect, useState } from "react";
import { getPortalDataSource } from "@/lib/data";
import type { Partner } from "@/lib/data/types";
import type { PartnerBusinessModuleId } from "@/lib/rbac/partnerModules";
import { partnersInModule } from "@/lib/rbac/partnerModules";

/** Resolves partner ids for module-scoped or single-partner screens. */
export function useScopePartnerIds(
  moduleId?: PartnerBusinessModuleId,
  partnerId?: string,
): string[] | undefined {
  const [partners, setPartners] = useState<Partner[]>([]);

  useEffect(() => {
    void getPortalDataSource()
      .getPartners()
      .then(setPartners);
  }, []);

  if (partnerId) return [partnerId];
  if (moduleId) return partnersInModule(partners, moduleId).map((p) => p.id);
  return undefined;
}

export function filterByPartnerIds<T extends { partnerId: string }>(
  items: T[],
  partnerIds: string[] | undefined,
): T[] {
  if (!partnerIds?.length) return items;
  const set = new Set(partnerIds);
  return items.filter((item) => set.has(item.partnerId));
}
