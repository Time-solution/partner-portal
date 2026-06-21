import { NavLink, useLocation } from "react-router-dom";
import type { ModuleScreenDef } from "@/lib/rbac/partnerModules";
import type { PartnerBusinessModuleId } from "@/lib/rbac/partnerModules";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { isPathActive, tabBarClass, tabLinkClass } from "@/lib/ui/tabs";

interface ModuleSubNavProps {
  moduleId: PartnerBusinessModuleId;
  screens: ModuleScreenDef[];
  lang: Lang;
  basePath?: string;
}

export function ModuleSubNav({ moduleId, screens, lang, basePath }: ModuleSubNavProps) {
  const t = useTranslator(lang);
  const { pathname } = useLocation();
  const root = basePath ?? `/modules/${moduleId}`;

  return (
    <nav className={tabBarClass} aria-label={t("moduleScreensNav" as never)}>
      {screens.map((screen) => {
        const to = `${root}/${screen.path}`;
        // One shared active-state predicate (same as sidebar + finance tabs) → consistent Zahy-green pill.
        return (
          <NavLink key={screen.id} to={to} className={tabLinkClass(isPathActive(pathname, to))}>
            {t(screen.labelKey as never)}
          </NavLink>
        );
      })}
    </nav>
  );
}
