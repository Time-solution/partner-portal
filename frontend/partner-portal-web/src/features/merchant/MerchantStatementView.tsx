import { useCallback, useEffect, useMemo, useState } from "react";
import { FileSpreadsheet, FileText, Loader2, Receipt } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import { subscribePortalDataChanged } from "@/lib/data/portalDataEvents";
import type { PortalData, SubscriptionBillingPeriod } from "@/lib/data/types";
import { assertMerchantRowFieldScope } from "@/lib/dashboard/merchantActivePartners";
import {
  buildMerchantRollupStatement,
  type MerchantRollupStatement,
} from "@/lib/statements/merchantRollupStatement";
import { filterByDate } from "@/lib/filters/dateRange";
import { invoiceProformaFromPeriod } from "@/lib/invoice/proformaSources";
import { downloadProformaPdf } from "@/lib/invoice/proformaPdf";
import { downloadProformaXlsx } from "@/lib/invoice/proformaExcel";
import { useDateRange } from "@/features/admin/hooks/useDateRange";
import { DateRangeFilter } from "@/features/admin/components/DateRangeFilter";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { activationStatusLabel } from "@/lib/i18n/domainLabels";

/**
 * MERCHANT STATEMENT view — a SALES-side roll-up across ALL the merchant's partners.
 *
 * Single source: {@link buildMerchantRollupStatement} (assembles the existing merchant dashboard
 * read model). Honors the shared `useDateRange`; the merchant finance-side scope is SALES-only by
 * construction (no buy/cost/margin field exists on a line). Per-partner export reuses the existing
 * proforma where a single billing period applies.
 */
export function MerchantStatementView({ lang, tenantId }: { lang: Lang; tenantId: string }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const { range, setRange } = useDateRange();
  const [data, setData] = useState<PortalData | null>(null);
  const [statement, setStatement] = useState<MerchantRollupStatement | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [all, partners] = await Promise.all([ds.getAll(), ds.getPartners()]);
      const partnerNameById = new Map(partners.map((p) => [p.id, p.tradeName ?? p.legalName]));
      const built = buildMerchantRollupStatement(all, tenantId, partnerNameById, range);
      for (const line of built.lines) assertMerchantRowFieldScope(line);
      setData(all);
      setStatement(built);
    } finally {
      setLoading(false);
    }
  }, [tenantId, range]);

  useEffect(() => {
    void load();
    return subscribePortalDataChanged(() => {
      void load();
    });
  }, [load]);

  /** In-range billing periods for one partner line — drives the per-partner proforma export. */
  const periodsFor = useMemo(() => {
    return (partnerId: string): SubscriptionBillingPeriod[] => {
      if (!data) return [];
      const own = data.billingPeriods.filter(
        (p) => p.tenantId === tenantId && p.partnerId === partnerId,
      );
      return filterByDate(own, range, (p) => p.periodKey);
    };
  }, [data, tenantId, range]);

  if (loading || !statement) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  return (
    <section className="space-y-4" dir={isRtl ? "rtl" : "ltr"} aria-labelledby="merchant-statement-title">
      <div className="flex flex-wrap items-center gap-2">
        <Receipt className="h-5 w-5 text-teal-600" aria-hidden="true" />
        <div>
          <h2 id="merchant-statement-title" className="text-lg font-semibold">
            {t("merchantStatementTitle" as never)}
          </h2>
          <p className="text-sm text-muted-foreground">{t("merchantStatementDesc" as never)}</p>
        </div>
        <BetaBadge lang={lang} />
      </div>

      <DateRangeFilter range={range} onChange={setRange} lang={lang} />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">{t("merchantStatementCombined" as never)}</CardTitle>
          <CardDescription>{t("merchantActivePartnersSummaryDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 text-sm sm:grid-cols-3">
          <div>
            <span className="text-muted-foreground">{t("statementTotalBilled" as never)}</span>
            <p className="text-xl font-semibold">
              <MoneyAmount amount={statement.totals.billed} />
            </p>
          </div>
          <div>
            <span className="text-muted-foreground">{t("statementTotalPaid" as never)}</span>
            <p className="text-xl font-semibold">
              <MoneyAmount amount={statement.totals.paid} />
            </p>
          </div>
          <div>
            <span className="text-muted-foreground">{t("statementTotalRemaining" as never)}</span>
            <p className="text-xl font-semibold">
              <MoneyAmount amount={statement.totals.remaining} />
            </p>
          </div>
        </CardContent>
      </Card>

      {statement.lines.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("merchantStatementEmpty" as never)}</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-border">
          <table className="w-full min-w-[720px] text-sm">
            <thead className="bg-muted/50 text-muted-foreground">
              <tr>
                <th className="px-3 py-2 text-start font-medium">{t("colTierService" as never)}</th>
                <th className="px-3 py-2 text-start font-medium">{t("colStatus" as never)}</th>
                <th className="px-3 py-2 text-end font-medium">{t("statementColBilled" as never)}</th>
                <th className="px-3 py-2 text-end font-medium">{t("statementColPaid" as never)}</th>
                <th className="px-3 py-2 text-end font-medium">{t("statementColRemaining" as never)}</th>
                <th className="px-3 py-2 text-end font-medium" />
              </tr>
            </thead>
            <tbody>
              {statement.lines.map((line) => {
                const periods = periodsFor(line.partnerId);
                const exportPeriod = periods[0];
                return (
                  <tr key={line.activationId} className="border-t border-border align-top">
                    <td className="px-3 py-2">
                      <p className="font-medium">{line.partnerName}</p>
                      <p className="text-xs text-muted-foreground">
                        {line.tierName}
                        {line.tierCode ? <span className="ms-1 font-mono">({line.tierCode})</span> : null}
                      </p>
                    </td>
                    <td className="px-3 py-2">{activationStatusLabel(lang, line.status)}</td>
                    <td className="px-3 py-2 text-end">
                      <MoneyAmount amount={line.billed} />
                    </td>
                    <td className="px-3 py-2 text-end">
                      <MoneyAmount amount={line.payment.paidToDate} />
                    </td>
                    <td className="px-3 py-2 text-end font-medium">
                      <MoneyAmount amount={line.payment.remaining} />
                    </td>
                    <td className="px-3 py-2">
                      {exportPeriod ? (
                        <div className="flex flex-wrap justify-end gap-2">
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => void downloadProformaPdf(invoiceProformaFromPeriod(exportPeriod, lang))}
                          >
                            <FileText className="h-4 w-4" />
                            {t("statementExportPdf" as never)}
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => void downloadProformaXlsx(invoiceProformaFromPeriod(exportPeriod, lang))}
                          >
                            <FileSpreadsheet className="h-4 w-4" />
                            {t("statementExportExcel" as never)}
                          </Button>
                        </div>
                      ) : (
                        <span className="block text-end text-xs text-muted-foreground">—</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <p className="text-xs text-muted-foreground">{t("partnerActiveMerchantsReadOnlyNote" as never)}</p>
    </section>
  );
}
