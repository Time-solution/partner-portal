import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "../components/PageHeader";
import { useTranslator } from "@/lib/i18n";
import type { ModuleScopeProps } from "../moduleScope";

export function PosSyncPage({ lang, moduleId, partnerId }: ModuleScopeProps) {
  const t = useTranslator(lang);

  return (
    <div className="space-y-6">
      {!moduleId && !partnerId ? (
        <PageHeader title={t("moduleScreen_posSync" as never)} description={t("posSyncDesc" as never)} lang={lang} />
      ) : null}
      <Card>
        <CardHeader>
          <CardTitle>{t("posSyncTitle" as never)}</CardTitle>
          <CardDescription>{t("posSyncDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">{t("mockSampleNotice" as never)}</p>
        </CardContent>
      </Card>
    </div>
  );
}
