import { NavLink } from "react-router-dom";
import type { ModuleScreenDef } from "@/lib/rbac/partnerModules";
import type { PartnerBusinessModuleId } from "@/lib/rbac/partnerModules";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

interface ModuleSubNavProps {
  moduleId: PartnerBusinessModuleId;
  screens: ModuleScreenDef[];
  lang: Lang;
  basePath?: string;
}

export function ModuleSubNav({ moduleId, screens, lang, basePath }: ModuleSubNavProps) {
  const t = useTranslator(lang);
  const root = basePath ?? `/modules/${moduleId}`;

  return (
    <nav
      className="flex flex-wrap gap-1 border-b border-border pb-2"
      aria-label={t("moduleScreensNav" as never)}
    >
      {screens.map((screen) => (
        <NavLink
          key={screen.id}
          to={`${root}/${screen.path}`}
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
