import { useCallback, useEffect, useState } from "react";
import { Loader2, Users } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import { subscribePortalDataChanged } from "@/lib/data/portalDataEvents";
import {
  assertPartnerRowFieldScope,
  buildPartnerActiveMerchants,
  type PartnerActiveMerchantsView,
} from "@/lib/dashboard/partnerActiveMerchants";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { activationStatusLabel } from "@/lib/i18n/domainLabels";

export function PartnerActiveMerchantsPanel({
  lang,
  partnerId,
}: {
  lang: Lang;
  partnerId: string;
}) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const [view, setView] = useState<PartnerActiveMerchantsView | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getPortalDataSource().getAll();
      const built = buildPartnerActiveMerchants(data, partnerId);
      for (const row of built.rows) {
        assertPartnerRowFieldScope(row);
      }
      setView(built);
    } finally {
      setLoading(false);
    }
  }, [partnerId]);

  useEffect(() => {
    void load();
    return subscribePortalDataChanged(() => {
      void load();
    });
  }, [load]);

  if (loading) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  if (!view) return null;

  return (
    <section className="space-y-4" dir={isRtl ? "rtl" : "ltr"} aria-labelledby="partner-active-merchants-title">
      <div className="flex flex-wrap items-center gap-2">
        <Users className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
        <div>
          <h2 id="partner-active-merchants-title" className="text-lg font-semibold">
            {t("partnerActiveMerchantsTitle" as never)}
          </h2>
          <p className="text-sm text-muted-foreground">{t("partnerActiveMerchantsDesc" as never)}</p>
        </div>
        <BetaBadge lang={lang} />
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">{t("partnerActiveMerchantsSummary" as never)}</CardTitle>
          <CardDescription>{t("partnerActiveMerchantsSummaryDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 sm:grid-cols-3 text-sm">
          <div>
            <span className="text-muted-foreground">{t("partnerActiveMerchantsCount" as never)}</span>
            <p className="text-xl font-semibold">{view.rows.length}</p>
          </div>
          <div>
            <span className="text-muted-foreground">{t("partnerPayableOwed" as never)}</span>
            <p className="text-xl font-semibold">
              <MoneyAmount amount={view.partnerPayableTotal} />
            </p>
          </div>
          <div>
            <span className="text-muted-foreground">{t("partnerPayableRemaining" as never)}</span>
            <p className="text-xl font-semibold">
              <MoneyAmount amount={view.partnerPayableTotal} />
            </p>
          </div>
        </CardContent>
      </Card>

      {view.rows.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("partnerActiveMerchantsEmpty" as never)}</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-border">
          <table className="w-full min-w-[640px] text-sm">
            <thead className="bg-muted/50 text-muted-foreground">
              <tr>
                <th className="px-3 py-2 text-start font-medium">{t("colMerchant" as never)}</th>
                <th className="px-3 py-2 text-start font-medium">{t("colTierService" as never)}</th>
                <th className="px-3 py-2 text-start font-medium">{t("colActiveSince" as never)}</th>
                <th className="px-3 py-2 text-start font-medium">{t("colStatus" as never)}</th>
                <th className="px-3 py-2 text-end font-medium">{t("partnerPayableOwed" as never)}</th>
                <th className="px-3 py-2 text-end font-medium">{t("partnerPayableDisbursed" as never)}</th>
                <th className="px-3 py-2 text-end font-medium">{t("partnerPayableRemaining" as never)}</th>
              </tr>
            </thead>
            <tbody>
              {view.rows.map((row) => (
                <tr key={row.activationId} className="border-t border-border">
                  <td className="px-3 py-2 font-medium">{row.merchantName}</td>
                  <td className="px-3 py-2">
                    <span>{row.tierName}</span>
                    {row.tierCode ? (
                      <span className="ms-1 font-mono text-xs text-muted-foreground">{row.tierCode}</span>
                    ) : null}
                  </td>
                  <td className="px-3 py-2 text-muted-foreground">
                    {row.activeSince
                      ? new Date(row.activeSince).toLocaleDateString(isRtl ? "ar-SA" : "en-GB")
                      : "—"}
                  </td>
                  <td className="px-3 py-2">{activationStatusLabel(lang, row.status)}</td>
                  <td className="px-3 py-2 text-end">
                    <MoneyAmount amount={row.payable.owed} />
                  </td>
                  <td className="px-3 py-2 text-end">
                    <MoneyAmount amount={row.payable.disbursed} />
                  </td>
                  <td className="px-3 py-2 text-end font-medium">
                    <MoneyAmount amount={row.payable.remainingToDisburse} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <p className="text-xs text-muted-foreground">{t("partnerActiveMerchantsReadOnlyNote" as never)}</p>
    </section>
  );
}
