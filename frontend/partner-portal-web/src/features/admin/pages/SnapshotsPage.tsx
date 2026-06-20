import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "../components/PageHeader";
import { useTranslator } from "@/lib/i18n";
import type { ModuleScopeProps } from "../moduleScope";

export function SnapshotsPage({ lang, moduleId, partnerId }: ModuleScopeProps) {
  const t = useTranslator(lang);

  return (
    <div className="space-y-6">
      {!moduleId && !partnerId ? (
        <PageHeader
          title={t("moduleScreen_snapshots" as never)}
          description={t("snapshotsDesc" as never)}
          lang={lang}
        />
      ) : null}
      <Card>
        <CardHeader>
          <CardTitle>{t("navSnapshots" as never)}</CardTitle>
          <CardDescription>{t("snapshotsDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">{t("mockSampleNotice" as never)}</p>
        </CardContent>
      </Card>
    </div>
  );
}
