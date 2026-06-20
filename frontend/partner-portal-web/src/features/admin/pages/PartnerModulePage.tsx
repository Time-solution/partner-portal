import { Navigate, useLocation, useParams } from "react-router-dom";
import { usePortalSession } from "@/features/auth/usePortalSession";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import {
  MODULE_BY_ID,
  type PartnerBusinessModuleId,
  getVisibleModuleScreens,
} from "@/lib/rbac/partnerModules";
import { ModuleSubNav } from "../components/ModuleSubNav";
import { PageHeader } from "../components/PageHeader";
import { ModuleScreenRenderer } from "../moduleScreenRegistry";
import { ModuleComingSoonPage } from "./ModuleComingSoonPage";

const MODULE_IDS: PartnerBusinessModuleId[] = [
  "delivery-service",
  "commerce",
  "fnb",
  "subscriptions",
  "marketplace",
  "consignment",
];

function isModuleId(value: string): value is PartnerBusinessModuleId {
  return MODULE_IDS.includes(value as PartnerBusinessModuleId);
}

export function PartnerModulePage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const location = useLocation();
  const { moduleId: moduleParam } = useParams<{ moduleId: string }>();
  const { user } = usePortalSession();

  const screenPath =
    moduleParam && location.pathname.startsWith(`/modules/${moduleParam}/`)
      ? location.pathname.slice(`/modules/${moduleParam}/`.length).split("/")[0]
      : undefined;

  if (!moduleParam || !isModuleId(moduleParam)) {
    return <Navigate to="/dashboard" replace />;
  }

  const moduleId = moduleParam;
  const mod = MODULE_BY_ID[moduleId];

  if (mod.comingSoon) {
    return <ModuleComingSoonPage lang={lang} moduleId={moduleId} />;
  }

  const screens = getVisibleModuleScreens(moduleId, user?.permissions ?? []);
  const defaultPath = screens[0]?.path;

  if (!screenPath && defaultPath) {
    return <Navigate to={`/modules/${moduleId}/${defaultPath}`} replace />;
  }

  const active = screens.find((s) => s.path === screenPath);
  if (!active) {
    return defaultPath ? (
      <Navigate to={`/modules/${moduleId}/${defaultPath}`} replace />
    ) : (
      <Navigate to="/dashboard" replace />
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t(mod.labelKey as never)} description={t(mod.descKey as never)} lang={lang} />
      <ModuleSubNav moduleId={moduleId} screens={screens} lang={lang} />
      <ModuleScreenRenderer
        screenId={active.id}
        context={{ lang, moduleId }}
      />
    </div>
  );
}
