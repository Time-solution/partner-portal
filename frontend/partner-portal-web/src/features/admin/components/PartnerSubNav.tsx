import { NavLink } from "react-router-dom";
import type { Partner } from "@/lib/data/types";
import {
  getVisibleModuleScreens,
  resolvePartnerModule,
} from "@/lib/rbac/partnerModules";
import { usePortalSession } from "@/features/auth/usePortalSession";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

interface PartnerSubNavProps {
  partner: Partner;
  lang: Lang;
}

/** Partner detail sub-nav — driven by business module config map. */
export function PartnerSubNav({ partner, lang }: PartnerSubNavProps) {
  const t = useTranslator(lang);
  const { user } = usePortalSession();
  const moduleId = resolvePartnerModule(partner);
  const screens = getVisibleModuleScreens(moduleId, user?.permissions ?? []);

  return (
    <nav
      className="flex flex-wrap gap-1 border-b border-border pb-2"
      aria-label={t("partnerSectionsNav" as never)}
    >
      {screens.map((screen) => (
        <NavLink
          key={screen.id}
          to={`/partners/${partner.id}/${screen.path}`}
          end={false}
          className={({ isActive }) =>
            [
              "rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
              isActive
                ? "bg-primary/10 text-primary"
                : "text-muted-foreground hover:bg-muted hover:text-foreground",
            ].join(" ")
          }
        >
          {t(screen.labelKey as never)}
        </NavLink>
      ))}
    </nav>
  );
}
