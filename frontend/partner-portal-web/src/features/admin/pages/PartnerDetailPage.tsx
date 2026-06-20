import { useEffect, useState } from "react";
import { Link, Navigate, useParams } from "react-router-dom";
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
import { ModuleSubNav } from "../components/ModuleSubNav";
import { ModuleScreenRenderer } from "../moduleScreenRegistry";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { participationModeLabel, partnerStatusLabel } from "@/lib/i18n/domainLabels";
import { ModuleComingSoonPage } from "./ModuleComingSoonPage";

export function PartnerDetailPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { id, tab } = useParams<{ id: string; tab?: string }>();
  const { scopedPartnerId, user } = usePortalSession();
  const [partner, setPartner] = useState<Partner | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) return;
    void getPortalDataSource()
      .getPartner(id)
      .then((p) => setPartner(p ?? null))
      .finally(() => setLoading(false));
  }, [id]);

  if (scopedPartnerId && id !== scopedPartnerId) {
    return <Navigate to={`/partners/${scopedPartnerId}`} replace />;
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
        <Button variant="ghost" size="sm" asChild>
          <Link to="/partners">
            <ArrowLeft className="h-4 w-4" aria-hidden="true" />
            {t("navPartners" as never)}
          </Link>
        </Button>
        <ModuleComingSoonPage lang={lang} moduleId={moduleId} />
      </div>
    );
  }

  const screens = getVisibleModuleScreens(moduleId, user?.permissions ?? []);
  const defaultTab = screens[0]?.path;

  if (!tab && defaultTab) {
    return <Navigate to={`/partners/${id}/${defaultTab}`} replace />;
  }

  const active = screens.find((s) => s.path === tab);
  if (!active) {
    return defaultTab ? <Navigate to={`/partners/${id}/${defaultTab}`} replace /> : null;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" asChild>
          <Link to="/partners">
            <ArrowLeft className="h-4 w-4" aria-hidden="true" />
            {t("navPartners" as never)}
          </Link>
        </Button>
      </div>

      <PageHeader
        title={partner.tradeName ?? partner.legalName}
        description={`${t(moduleLabelKey(moduleId) as never)} · ${partnerTypeLabel(partner.type, lang)} · ${participationModeLabel(lang, partner.participationMode)} · ${partnerStatusLabel(lang, partner.status)}`}
        lang={lang}
      />

      <ModuleSubNav moduleId={moduleId} screens={screens} lang={lang} basePath={`/partners/${id}`} />
      <ModuleScreenRenderer
        screenId={active.id}
        context={{ lang, partnerId: id, moduleId }}
      />
    </div>
  );
}
