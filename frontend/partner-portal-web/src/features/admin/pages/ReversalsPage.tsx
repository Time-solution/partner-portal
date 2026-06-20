import { useCallback, useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { SettlementReversal } from "@/lib/data/types";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { JournalTable } from "../components/JournalTable";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

export function ReversalsPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { scopedPartnerId } = usePortalSession();
  const [reversals, setReversals] = useState<SettlementReversal[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setReversals(await getPortalDataSource().getReversals(scopedPartnerId));
    } finally {
      setLoading(false);
    }
  }, [scopedPartnerId]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("navReversals" as never)}
        description={t("reversalsDesc" as never)}
        lang={lang}
        showBeta
      />
      {loading ? (
        <div className="flex items-center gap-2 text-base text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          {t("loadingData" as never)}
        </div>
      ) : reversals.length === 0 ? (
        <p className="text-base text-muted-foreground">{t("reversalsEmpty" as never)}</p>
      ) : (
        <div className="space-y-4">
          {reversals.map((r) => (
            <Card key={r.id}>
              <CardHeader>
                <CardTitle className="text-lg">{r.partnerName}</CardTitle>
                <CardDescription className="text-base">
                  {t("reversalOriginal" as never)}: {r.originalCaseId} ·{" "}
                  {r.netsToZero ? (
                    <span className="text-emerald-600">{t("netsToZero" as never)}</span>
                  ) : (
                    "—"
                  )}
                </CardDescription>
              </CardHeader>
              <CardContent>
                <JournalTable lines={r.journal.lines} caption={t("reversalPerLine" as never)} />
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
