import { NavLink, useLocation } from "react-router-dom";
import type { Partner } from "@/lib/data/types";
import {
  getVisibleModuleScreens,
  resolvePartnerModule,
} from "@/lib/rbac/partnerModules";
import { usePortalSession } from "@/features/auth/usePortalSession";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { isPathActive, tabBarClass, tabLinkClass } from "@/lib/ui/tabs";

interface PartnerSubNavProps {
  partner: Partner;
  lang: Lang;
}

/** Partner detail sub-nav — driven by business module config map. */
export function PartnerSubNav({ partner, lang }: PartnerSubNavProps) {
  const t = useTranslator(lang);
  const { user } = usePortalSession();
  const { pathname } = useLocation();
  const moduleId = resolvePartnerModule(partner);
  const screens = getVisibleModuleScreens(moduleId, user?.permissions ?? []);

  return (
    <nav className={tabBarClass} aria-label={t("partnerSectionsNav" as never)}>
      {screens.map((screen) => {
        const to = `/partners/${partner.id}/${screen.path}`;
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
