import type { Lang } from "@/lib/i18n";
import type { PartnerBusinessModuleId } from "@/lib/rbac/partnerModules";

/** Shared props for config-driven module screens. */
export interface ModuleScopeProps {
  lang: Lang;
  moduleId?: PartnerBusinessModuleId;
  partnerId?: string;
  /** Finance workspace — reconcile/read only; disburse hidden. */
  financeMode?: boolean;
}
