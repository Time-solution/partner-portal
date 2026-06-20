import type { Lang } from "@/lib/i18n";
import { roleBadgeClass, roleLabels, type PortalRole } from "@/lib/rbac/portalRoles";

export function RoleBadge({ role, lang }: { role: PortalRole; lang: Lang }) {
  return (
    <span
      className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium ${roleBadgeClass[role]}`}
    >
      {roleLabels[role][lang]}
    </span>
  );
}
