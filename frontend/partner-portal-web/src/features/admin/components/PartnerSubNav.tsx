import { NavLink } from "react-router-dom";
import { getPartnerSubTabs } from "@/lib/rbac/partnerNav";
import type { Partner } from "@/lib/data/types";
import type { Lang } from "@/lib/i18n";

interface PartnerSubNavProps {
  partner: Partner;
  lang: Lang;
}

export function PartnerSubNav({ partner, lang }: PartnerSubNavProps) {
  const tabs = getPartnerSubTabs(partner.type, partner.participationMode);

  return (
    <nav
      className="flex flex-wrap gap-1 border-b border-border pb-2"
      aria-label={lang === "ar" ? "أقسام الشريك" : "Partner sections"}
    >
      {tabs.map((tab) => (
        <NavLink
          key={tab.key}
          to={`/partners/${partner.id}/${tab.path}`}
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
          {lang === "ar" ? tab.labelAr : tab.labelEn}
        </NavLink>
      ))}
    </nav>
  );
}
