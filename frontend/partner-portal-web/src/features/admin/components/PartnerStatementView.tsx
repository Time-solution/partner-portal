import { useCallback, useEffect, useState } from "react";
import { FileSpreadsheet, FileText, Loader2, Receipt } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import { subscribePortalDataChanged } from "@/lib/data/portalDataEvents";
import { assertPartnerRowFieldScope } from "@/lib/dashboard/partnerActiveMerchants";
import {
  buildPartnerRollupStatement,
  type PartnerRollupStatement,
} from "@/lib/statements/partnerRollupStatement";
import { scopeFinancialsByDate } from "@/lib/statements/dateScopedData";
import { partnerStatement, toReportEntries } from "@/lib/reports/settlementReports";
import { statementProformaFromPartner } from "@/lib/invoice/proformaSources";
import { downloadProformaPdf } from "@/lib/invoice/proformaPdf";
import { downloadProformaXlsx } from "@/lib/invoice/proformaExcel";
import { useDateRange } from "@/features/admin/hooks/useDateRange";
import { DateRangeFilter } from "@/features/admin/components/DateRangeFilter";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { activationStatusLabel } from "@/lib/i18n/domainLabels";
import {
  splitPartnerPayableInclusive,
  type PartnerPayableVatSplit,
} from "@/lib/statements/partnerPayableVat";

function PartnerPayableVatSplitLine({
  split,
  lang,
}: {
  split: PartnerPayableVatSplit;
  lang: Lang;
}) {
  const t = useTranslator(lang);
  if (!split.inclusive) return null;
  return (
    <p className="mt-1 text-xs text-muted-foreground tabular-nums">
      {t("partnerStatementExVat" as never)}: <MoneyAmount amount={split.exVat} />
      {" · "}
      {t("partnerStatementInputVat" as never)}: <MoneyAmount amount={split.inputVat} />
      {" · "}
      {t("partnerStatementInclVat" as never)}: <MoneyAmount amount={split.inclusive} />
    </p>
  );
}

/**
 * PARTNER STATEMENT view — a PURCHASE/payable-side statement (Direction 2).
 *
 * Single source: {@link buildPartnerRollupStatement} (assembles the existing partner dashboard
 * read model). Honors the shared `useDateRange`; the partner finance-side scope is PURCHASE-only by
 * construction (no sell/resale/margin/receivable field exists on a line). Export reuses the existing
 * Direction-2 statement proforma.
 */
export function PartnerStatementView({ lang, partnerId }: { lang: Lang; partnerId: string }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const { range, setRange } = useDateRange();
  const [statement, setStatement] = useState<PartnerRollupStatement | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getPortalDataSource().getAll();
      const built = buildPartnerRollupStatement(data, partnerId, range);
      for (const line of built.lines) assertPartnerRowFieldScope(line);
      setStatement(built);
    } finally {
      setLoading(false);
    }
  }, [partnerId, range]);

  useEffect(() => {
    void load();
    return subscribePortalDataChanged(() => {
      void load();
    });
  }, [load]);

  const exportStatement = useCallback(
    async (sink: "pdf" | "xlsx") => {
      const data = await getPortalDataSource().getAll();
      const scoped = scopeFinancialsByDate(data, range);
      const stmt = partnerStatement(toReportEntries(scoped), partnerId);
      const doc = statementProformaFromPartner(stmt, lang);
      if (sink === "pdf") await downloadProformaPdf(doc);
      else await downloadProformaXlsx(doc);
    },
    [partnerId, range, lang],
  );

  if (loading || !statement) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  return (
    <section className="space-y-4" dir={isRtl ? "rtl" : "ltr"} aria-labelledby="partner-statement-title">
      <div className="flex flex-wrap items-center gap-2">
        <Receipt className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
        <div>
          <h2 id="partner-statement-title" className="text-lg font-semibold">
            {t("partnerStatementTitle" as never)}
          </h2>
          <p className="text-sm text-muted-foreground">{t("partnerStatementDesc" as never)}</p>
        </div>
        <BetaBadge lang={lang} />
      </div>

      <div className="flex flex-wrap items-end justify-between gap-3">
        <DateRangeFilter range={range} onChange={setRange} lang={lang} />
        <div className="flex flex-wrap gap-2">
          <Button size="sm" variant="outline" onClick={() => void exportStatement("pdf")}>
            <FileText className="h-4 w-4" />
            {t("statementExportPdf" as never)}
          </Button>
          <Button size="sm" variant="outline" onClick={() => void exportStatement("xlsx")}>
            <FileSpreadsheet className="h-4 w-4" />
            {t("statementExportExcel" as never)}
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">{t("partnerActiveMerchantsSummary" as never)}</CardTitle>
          <CardDescription>{t("partnerActiveMerchantsSummaryDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 text-sm sm:grid-cols-3">
          <div>
            <span className="text-muted-foreground">{t("partnerPayableOwed" as never)}</span>
            <p className="text-xl font-semibold">
              <MoneyAmount amount={statement.totals.owed} />
            </p>
            <PartnerPayableVatSplitLine
              split={splitPartnerPayableInclusive(statement.totals.owed)}
              lang={lang}
            />
          </div>
          <div>
            <span className="text-muted-foreground">{t("partnerPayableDisbursed" as never)}</span>
            <p className="text-xl font-semibold">
              <MoneyAmount amount={statement.totals.disbursed} />
            </p>
          </div>
          <div>
            <span className="text-muted-foreground">{t("partnerPayableRemaining" as never)}</span>
            <p className="text-xl font-semibold">
              <MoneyAmount amount={statement.totals.remaining} />
            </p>
          </div>
        </CardContent>
      </Card>

      {statement.lines.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("partnerStatementEmpty" as never)}</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-border">
          <table className="w-full min-w-[640px] text-sm">
            <thead className="bg-muted/50 text-muted-foreground">
              <tr>
                <th className="px-3 py-2 text-start font-medium">{t("colMerchant" as never)}</th>
                <th className="px-3 py-2 text-start font-medium">{t("colTierService" as never)}</th>
                <th className="px-3 py-2 text-start font-medium">{t("colStatus" as never)}</th>
                <th className="px-3 py-2 text-end font-medium">{t("partnerPayableOwed" as never)}</th>
                <th className="px-3 py-2 text-end font-medium">{t("partnerPayableDisbursed" as never)}</th>
                <th className="px-3 py-2 text-end font-medium">{t("partnerPayableRemaining" as never)}</th>
              </tr>
            </thead>
            <tbody>
              {statement.lines.map((line) => (
                <tr key={line.activationId} className="border-t border-border">
                  <td className="px-3 py-2 font-medium">{line.merchantName}</td>
                  <td className="px-3 py-2">
                    <span>{line.tierName}</span>
                    {line.tierCode ? (
                      <span className="ms-1 font-mono text-xs text-muted-foreground">{line.tierCode}</span>
                    ) : null}
                  </td>
                  <td className="px-3 py-2">{activationStatusLabel(lang, line.status)}</td>
                  <td className="px-3 py-2 text-end">
                    <MoneyAmount amount={line.payable.owed} />
                    <PartnerPayableVatSplitLine
                      split={splitPartnerPayableInclusive(line.payable.owed)}
                      lang={lang}
                    />
                  </td>
                  <td className="px-3 py-2 text-end">
                    <MoneyAmount amount={line.payable.disbursed} />
                  </td>
                  <td className="px-3 py-2 text-end font-medium">
                    <MoneyAmount amount={line.payable.remainingToDisburse} />
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
