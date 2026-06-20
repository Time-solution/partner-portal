import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { PORTAL_ROLES, roleLabels, type PortalRole } from "@/lib/rbac/portalRoles";

interface MockRoleSwitcherProps {
  role: PortalRole;
  onChange: (role: PortalRole) => void;
  lang: Lang;
}

/** Dev-only mock role switcher — not wired to real auth. */
export function MockRoleSwitcher({ role, onChange, lang }: MockRoleSwitcherProps) {
  const t = useTranslator(lang);

  return (
    <label className="flex items-center gap-2 text-xs text-muted-foreground">
      <span className="hidden lg:inline">{t("mockRoleLabel" as never)}</span>
      <select
        value={role}
        onChange={(e) => onChange(e.target.value as PortalRole)}
        className="rounded-md border border-border bg-background px-2 py-1 text-xs text-foreground"
        aria-label={t("mockSwitchRole" as never)}
      >
        {PORTAL_ROLES.map((r) => (
          <option key={r} value={r}>
            {roleLabels[r][lang]}
          </option>
        ))}
      </select>
    </label>
  );
}
