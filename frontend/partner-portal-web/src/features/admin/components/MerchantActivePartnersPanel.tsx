import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, Loader2, Sparkles } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import { subscribePortalDataChanged } from "@/lib/data/portalDataEvents";
import {
  assertMerchantRowFieldScope,
  buildMerchantActivePartners,
  type MerchantActivePartnerRow,
} from "@/lib/dashboard/merchantActivePartners";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { activationStatusLabel } from "@/lib/i18n/domainLabels";

export function MerchantActivePartnersPanel({
  lang,
  tenantId,
  onDeactivate,
  busyActivationId,
}: {
  lang: Lang;
  tenantId: string;
  onDeactivate?: (activationId: string) => void;
  busyActivationId?: string | null;
}) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const [rows, setRows] = useState<MerchantActivePartnerRow[]>([]);
  const [briefByPartner, setBriefByPartner] = useState<Map<string, string>>(new Map());
  const [benefitByKey, setBenefitByKey] = useState<Map<string, string>>(new Map());
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [data, partners] = await Promise.all([ds.getAll(), ds.getPartners()]);
      const partnerNameById = new Map(
        partners.map((p) => [p.id, p.tradeName ?? p.legalName]),
      );
      // Phase 6b — read-only presentation fields (no pricing): company brief + offering benefit.
      setBriefByPartner(
        new Map(partners.filter((p) => p.partnerBrief).map((p) => [p.id, p.partnerBrief as string])),
      );
      setBenefitByKey(
        new Map(
          data.catalogItems
            .filter((c) => c.merchantBenefit)
            .map((c) => [`${c.partnerId}:${c.code}`, c.merchantBenefit as string]),
        ),
      );
      const built = buildMerchantActivePartners(data, tenantId, partnerNameById);
      for (const row of built) {
        assertMerchantRowFieldScope(row);
      }
      setRows(built);
    } finally {
      setLoading(false);
    }
  }, [tenantId]);

  useEffect(() => {
    void load();
    return subscribePortalDataChanged(() => {
      void load();
    });
  }, [load]);

  const activeCount = useMemo(
    () => rows.filter((r) => r.status === "Active").length,
    [rows],
  );

  if (loading) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  return (
    <section className="space-y-4" dir={isRtl ? "rtl" : "ltr"} aria-labelledby="merchant-active-partners-title">
      <div className="flex flex-wrap items-center gap-2">
        <Sparkles className="h-5 w-5 text-teal-600" aria-hidden="true" />
        <div>
          <h2 id="merchant-active-partners-title" className="text-lg font-semibold">
            {t("merchantActivePartnersTitle" as never)}
          </h2>
          <p className="text-sm text-muted-foreground">{t("merchantActivePartnersDesc" as never)}</p>
        </div>
        <BetaBadge lang={lang} />
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">{t("merchantActivePartnersSummary" as never)}</CardTitle>
          <CardDescription>{t("merchantActivePartnersSummaryDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-sm">
            <span className="text-muted-foreground">{t("merchantActivePartnersCount" as never)}: </span>
            <span className="font-semibold">{activeCount}</span>
          </p>
        </CardContent>
      </Card>

      {rows.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("merchantActivePartnersEmpty" as never)}</p>
      ) : (
        <div className="space-y-3">
          {rows.map((row) => (
            <Card key={row.activationId}>
              <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3 pb-2">
                <div>
                  <CardTitle className="text-base">{row.partnerName}</CardTitle>
                  <CardDescription className="text-sm">
                    {row.tierName}
                    {row.tierCode ? (
                      <span className="ms-1 font-mono text-xs">({row.tierCode})</span>
                    ) : null}
                    {" · "}
                    {activationStatusLabel(lang, row.status)}
                    {row.activeSince ? (
                      <>
                        {" · "}
                        {t("colActiveSince" as never)}:{" "}
                        {new Date(row.activeSince).toLocaleDateString(isRtl ? "ar-SA" : "en-GB")}
                      </>
                    ) : null}
                  </CardDescription>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  {row.status === "Active" ? (
                    <span className="inline-flex items-center gap-1 rounded-full border border-teal-500/40 bg-teal-500/10 px-2.5 py-1 text-xs font-medium text-teal-800 dark:text-teal-200">
                      <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" />
                      {t("merchantPreviewInstantActive" as never)}
                    </span>
                  ) : null}
                  {row.status !== "Ended" && onDeactivate ? (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={busyActivationId === row.activationId}
                      onClick={() => onDeactivate(row.activationId)}
                    >
                      {t("merchantPreviewEnd" as never)}
                    </Button>
                  ) : null}
                </div>
              </CardHeader>
              {briefByPartner.get(row.partnerId) || benefitByKey.get(`${row.partnerId}:${row.tierCode}`) ? (
                <CardContent className="space-y-2 pb-0 pt-0" data-testid="active-partner-presentation">
                  {briefByPartner.get(row.partnerId) ? (
                    <div>
                      <p className="text-xs font-medium text-muted-foreground">
                        {t("merchantBrowseAboutPartner" as never)}
                      </p>
                      <p className="mt-0.5 whitespace-pre-line text-sm">{briefByPartner.get(row.partnerId)}</p>
                    </div>
                  ) : null}
                  {benefitByKey.get(`${row.partnerId}:${row.tierCode}`) ? (
                    <div>
                      <p className="text-xs font-medium text-teal-700 dark:text-teal-300">
                        {t("merchantBrowseWhatYouGet" as never)}
                      </p>
                      <p className="mt-0.5 text-sm">{benefitByKey.get(`${row.partnerId}:${row.tierCode}`)}</p>
                    </div>
                  ) : null}
                </CardContent>
              ) : null}
              <CardContent className="grid gap-2 pt-0 text-sm sm:grid-cols-2 lg:grid-cols-4">
                <div>
                  <span className="text-muted-foreground">{t("merchantBrowseYourPrice" as never)}</span>
                  <p className="font-medium">
                    <MoneyAmount amount={row.sellPrice.amount} />
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">{t("merchantPaidToDate" as never)}</span>
                  <p className="font-medium">
                    <MoneyAmount amount={row.payment.paidToDate} />
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">{t("merchantRemainingDue" as never)}</span>
                  <p className="font-medium">
                    <MoneyAmount amount={row.payment.remaining} />
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">{t("colPaymentStatus" as never)}</span>
                  <p className="font-medium">
                    {row.payment.status === "None"
                      ? "—"
                      : t(`paymentStatus_${row.payment.status}` as never)}
                  </p>
                </div>
              </CardContent>
              {row.status !== "Ended" ? (
                <CardContent className="border-t border-border pt-3">
                  <p className="text-xs text-muted-foreground">
                    {t("merchantDeactivateProrationNote" as never)}
                  </p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {t("merchantDeactivateDisplayOnlyNote" as never)}
                  </p>
                </CardContent>
              ) : null}
            </Card>
          ))}
        </div>
      )}
    </section>
  );
}
