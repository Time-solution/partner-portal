import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import type { PartnerBusinessModuleId } from "@/lib/rbac/partnerModules";
import { MODULE_BY_ID } from "@/lib/rbac/partnerModules";

export function ModuleComingSoonPage({
  lang,
  moduleId,
}: {
  lang: Lang;
  moduleId: PartnerBusinessModuleId;
}) {
  const t = useTranslator(lang);
  const mod = MODULE_BY_ID[moduleId];

  return (
    <div className="space-y-6">
      <PageHeader
        title={t(mod.labelKey as never)}
        description={t(mod.descKey as never)}
        lang={lang}
      />
      <Card>
        <CardHeader>
          <CardTitle>{t("comingSoon" as never)}</CardTitle>
          <CardDescription>{t("module_consignmentSoonDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">{t("sectionPlaceholder" as never)}</p>
        </CardContent>
      </Card>
    </div>
  );
}
