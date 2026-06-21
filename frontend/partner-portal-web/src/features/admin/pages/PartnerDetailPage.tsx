import { useEffect, useState } from "react";
import { Link, Navigate, NavLink, useParams } from "react-router-dom";
import { ArrowLeft, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { Partner } from "@/lib/data/types";
import { partnerTypeLabel } from "@/lib/rbac/partnerNav";
import {
  MODULE_BY_ID,
  getVisibleModuleScreens,
  moduleLabelKey,
  resolvePartnerModule,
} from "@/lib/rbac/partnerModules";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { isPartnerScopedRole, partnerHomePath } from "@/lib/rbac/roleNavConfig";
import { ModuleScreenRenderer } from "../moduleScreenRegistry";
import { FinanceDrillDown } from "../components/FinanceDrillDown";
import { PartnerActiveMerchantsPanel } from "../components/PartnerActiveMerchantsPanel";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { participationModeLabel, partnerStatusLabel } from "@/lib/i18n/domainLabels";
import { ModuleComingSoonPage } from "./ModuleComingSoonPage";
import { OrgProfileForm } from "@/features/settings/profile/OrgProfileForm";
import { tabBarClass, tabLinkClass } from "@/lib/ui/tabs";

export function PartnerDetailPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { id, tab } = useParams<{ id: string; tab?: string }>();
  const { scopedPartnerId, user, role } = usePortalSession();
  const [partner, setPartner] = useState<Partner | null>(null);
  const [loading, setLoading] = useState(true);
  const partnerScoped = isPartnerScopedRole(role);

  useEffect(() => {
    if (!id) return;
    void getPortalDataSource()
      .getPartner(id)
      .then((p) => setPartner(p ?? null))
      .finally(() => setLoading(false));
  }, [id]);

  if (scopedPartnerId && id !== scopedPartnerId) {
    return <Navigate to={partnerHomePath(scopedPartnerId)} replace />;
  }

  if (loading) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  if (!partner || !id) {
    return (
      <Card>
        <CardContent className="py-8 text-sm text-muted-foreground">{t("partnerNotFound" as never)}</CardContent>
      </Card>
    );
  }

  const moduleId = resolvePartnerModule(partner);
  const mod = MODULE_BY_ID[moduleId];

  if (mod.comingSoon) {
    return (
      <div className="space-y-6">
        {!partnerScoped ? (
          <Button variant="ghost" size="sm" asChild>
            <Link to="/partners">
              <ArrowLeft className="h-4 w-4" aria-hidden="true" />
              {t("navPartners" as never)}
            </Link>
          </Button>
        ) : null}
        <ModuleComingSoonPage lang={lang} moduleId={moduleId} />
      </div>
    );
  }

  const screens = getVisibleModuleScreens(moduleId, user?.permissions ?? []);
  const defaultTab = partnerScoped ? "active-merchants" : screens[0]?.path;
  const showPartnerProfile = partnerScoped;

  if (tab === "profile" && !showPartnerProfile && defaultTab) {
    return <Navigate to={`/partners/${id}/${defaultTab}`} replace />;
  }

  if (showPartnerProfile && tab === "profile") {
    return (
      <div className="space-y-6">
        <PageHeader
          title={partner.tradeName ?? partner.legalName}
          description={t("orgProfilePartnerDesc" as never)}
          lang={lang}
        />
        <nav className={tabBarClass} aria-label={t("partnerTab_profile" as never)}>
          <NavLink
            to={`/partners/${id}/active-merchants`}
            className={({ isActive }) => tabLinkClass(isActive)}
          >
            {t("partnerActiveMerchantsTitle" as never)}
          </NavLink>
          <NavLink to={`/partners/${id}/profile`} className={() => tabLinkClass(true)}>
            {t("partnerTab_profile" as never)}
          </NavLink>
        </nav>
        <OrgProfileForm
          lang={lang}
          scope={{ kind: "partner", id }}
          requireNationalNumber
          titleKey="orgProfilePartnerTitle"
          descKey="orgProfilePartnerDesc"
        />
      </div>
    );
  }

  if (!tab && defaultTab) {
    return <Navigate to={`/partners/${id}/${defaultTab}`} replace />;
  }

  if (tab === "active-merchants") {
    return (
      <div className="space-y-6">
        <PageHeader
          title={partner.tradeName ?? partner.legalName}
          description={t("partnerActiveMerchantsPageDesc" as never)}
          lang={lang}
        />
        {partnerScoped ? (
          <nav className={tabBarClass} aria-label={t("partnerDashboardNav" as never)}>
            <NavLink
              to={`/partners/${id}/active-merchants`}
              className={() => tabLinkClass(true)}
            >
              {t("partnerActiveMerchantsTitle" as never)}
            </NavLink>
            <NavLink
              to={`/partners/${id}/profile`}
              className={({ isActive }) => tabLinkClass(isActive)}
            >
              {t("partnerTab_profile" as never)}
            </NavLink>
          </nav>
        ) : null}
        <PartnerActiveMerchantsPanel lang={lang} partnerId={id} />
      </div>
    );
  }

  const active = screens.find((s) => s.path === tab);
  if (!active) {
    return defaultTab ? <Navigate to={`/partners/${id}/${defaultTab}`} replace /> : null;
  }

  return (
    <div className="space-y-6">
      {!partnerScoped ? (
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" asChild>
            <Link to="/partners">
              <ArrowLeft className="h-4 w-4" aria-hidden="true" />
              {t("navPartners" as never)}
            </Link>
          </Button>
        </div>
      ) : null}

      <PageHeader
        title={partner.tradeName ?? partner.legalName}
        description={
          partnerScoped
            ? `${t("partnerHomeDesc" as never)} · ${t(moduleLabelKey(moduleId) as never)} · ${partnerTypeLabel(partner.type, lang)}`
            : `${t(moduleLabelKey(moduleId) as never)} · ${partnerTypeLabel(partner.type, lang)} · ${participationModeLabel(lang, partner.participationMode)} · ${partnerStatusLabel(lang, partner.status)}`
        }
        lang={lang}
      />

      {!partnerScoped ? <FinanceDrillDown lang={lang} /> : null}

      <nav className={tabBarClass} aria-label={t("moduleScreensNav" as never)}>
        {!partnerScoped ? (
          <NavLink
            to={`/partners/${id}/active-merchants`}
            className={({ isActive }) => tabLinkClass(isActive)}
          >
            {t("partnerActiveMerchantsTitle" as never)}
          </NavLink>
        ) : null}
        {screens.map((screen) => (
          <NavLink
            key={screen.id}
            to={`/partners/${id}/${screen.path}`}
            className={({ isActive }) => tabLinkClass(isActive)}
          >
            {t(screen.labelKey as never)}
          </NavLink>
        ))}
        {showPartnerProfile ? (
          <NavLink to={`/partners/${id}/profile`} className={({ isActive }) => tabLinkClass(isActive)}>
            {t("partnerTab_profile" as never)}
          </NavLink>
        ) : null}
      </nav>
      <ModuleScreenRenderer
        screenId={active.id}
        context={{ lang, partnerId: id, moduleId }}
      />
    </div>
  );
}
