import { useCallback, useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import { deriveSettlementSummary, type SettlementCase } from "@/lib/data/types";
import { canDisburse } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { JournalTable } from "../components/JournalTable";
import { PageHeader } from "../components/PageHeader";
import { useTranslator } from "@/lib/i18n";
import { settlementBookLabel, settlementStateLabel } from "@/lib/i18n/domainLabels";
import type { ModuleScopeProps } from "../moduleScope";
import { filterByPartnerIds, useScopePartnerIds } from "../hooks/useScopePartnerIds";

export function SettlementPage({ lang, moduleId, partnerId, financeMode }: ModuleScopeProps) {
  const t = useTranslator(lang);
  const { role, scopedPartnerId } = usePortalSession();
  const scopeIds = useScopePartnerIds(moduleId, partnerId ?? scopedPartnerId);
  const [cases, setCases] = useState<SettlementCase[]>([]);
  const [reversedIds, setReversedIds] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [caseList, reversals] = await Promise.all([
        ds.getSettlementCases(scopedPartnerId ?? partnerId),
        ds.getReversals(scopedPartnerId ?? partnerId),
      ]);
      setCases(filterByPartnerIds(caseList, scopeIds));
      setReversedIds(new Set(reversals.map((r) => r.originalCaseId)));
    } finally {
      setLoading(false);
    }
  }, [scopedPartnerId, partnerId, scopeIds?.join(",")]);

  useEffect(() => {
    void load();
  }, [load]);

  const showDisburse = canDisburse(role as never) && !financeMode;
  const canReverse =
    !financeMode && (role === "PlatformAdmin" || role === "Accountant");
  const showHeader = !moduleId && !partnerId && !financeMode;

  const triggerReversal = async (caseId: string) => {
    setBusyId(caseId);
    try {
      await getPortalDataSource().triggerReversal(caseId);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="space-y-6">
      {showHeader ? (
        <PageHeader
          title={t("navSettlement" as never)}
          description={t("settlementDesc" as never)}
          lang={lang}
          showBeta
        />
      ) : null}
      {loading ? (
        <div className="flex items-center gap-2 text-base text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          {t("loadingData" as never)}
        </div>
      ) : (
        <div className="space-y-4">
          {cases.map((c) => {
            const summary = deriveSettlementSummary(c.journal);
            const hasReversal = reversedIds.has(c.id);
            return (
              <Card key={c.id}>
                <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3">
                  <div>
                    <CardTitle className="text-lg">{c.partnerName}</CardTitle>
                    <CardDescription className="text-base">
                      {c.externalTransactionId} · {settlementStateLabel(lang, c.state)} ·{" "}
                      {settlementBookLabel(lang, c.book)}
                    </CardDescription>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {canReverse && !hasReversal ? (
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={busyId === c.id}
                        onClick={() => void triggerReversal(c.id)}
                      >
                        {t("triggerReversal" as never)}
                      </Button>
                    ) : hasReversal ? (
                      <span className="text-sm text-emerald-600">{t("reversalExists" as never)}</span>
                    ) : null}
                    {showDisburse ? (
                      <Button size="sm" disabled title={t("disburseDisabledDemo" as never)}>
                        {t("settlementDisburse" as never)}
                      </Button>
                    ) : (
                      <span className="text-sm text-muted-foreground">{t("disburseHidden" as never)}</span>
                    )}
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  <dl className="grid gap-3 text-base sm:grid-cols-2 lg:grid-cols-4">
                    <div>
                      <dt className="text-muted-foreground">{t("settlementBuy" as never)}</dt>
                      <dd className="font-medium tabular-nums">
                        {summary.buyPrice.amount.toFixed(2)} {summary.buyPrice.currency}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("settlementSell" as never)}</dt>
                      <dd className="font-medium tabular-nums">
                        {summary.sellPrice.amount.toFixed(2)} {summary.sellPrice.currency}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("settlementMargin" as never)}</dt>
                      <dd className="font-medium tabular-nums">{summary.margin.toFixed(2)} SAR</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("settlementNetVat" as never)}</dt>
                      <dd className="font-medium tabular-nums">{summary.netVatToZatca.toFixed(2)} SAR</dd>
                    </div>
                  </dl>
                  <JournalTable lang={lang} lines={c.journal.lines} caption={t("settlementJournalCaption" as never)} />
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
