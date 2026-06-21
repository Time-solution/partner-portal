import { useEffect, useMemo, useState } from "react";
import { Download, FileSpreadsheet, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import type { Receipt, SubscriptionBillingPeriod } from "@/lib/data/types";
import { deriveBalances } from "@/lib/data/types";
import { formatTenantRef } from "@/lib/format/recordId";
import { downloadMoneyCsv, downloadMoneyXlsx } from "@/lib/export/moneyExport";
import { useTranslator, type Lang } from "@/lib/i18n";

function SummaryCard({
  label,
  value,
  accent,
  plain,
}: {
  label: string;
  value: number;
  accent?: string;
  plain?: boolean;
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle>
      </CardHeader>
      <CardContent>
        <p className={`text-2xl font-bold tabular-nums ${accent ?? ""}`}>
          {plain ? value : <MoneyAmount amount={value} />}
        </p>
      </CardContent>
    </Card>
  );
}

export function BalancesPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const [periods, setPeriods] = useState<SubscriptionBillingPeriod[]>([]);
  const [receipts, setReceipts] = useState<Receipt[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const ds = getPortalDataSource();
    void Promise.all([ds.getBillingPeriods(), ds.getReceipts()])
      .then(([p, r]) => {
        setPeriods(p);
        setReceipts(r);
      })
      .finally(() => setLoading(false));
  }, []);

  const balances = useMemo(() => deriveBalances(periods), [periods]);
  const totals = useMemo(
    () =>
      balances.reduce(
        (acc, r) => ({
          invoiced: acc.invoiced + r.invoiced,
          paid: acc.paid + r.paid,
          outstanding: acc.outstanding + r.outstanding,
          overdue: acc.overdue + r.overdueCount,
        }),
        { invoiced: 0, paid: 0, outstanding: 0, overdue: 0 },
      ),
    [balances],
  );

  if (loading) {
    return (
      <div className="flex items-center gap-2 text-base text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="flex items-center gap-2 text-lg font-semibold">
            {t("balancesTitle" as never)}
            <BetaBadge lang={lang} />
          </h3>
          <p className="text-base text-muted-foreground">{t("balancesDesc" as never)}</p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={periods.length === 0}
            onClick={() => downloadMoneyCsv({ invoices: periods, receipts, lang })}
          >
            <Download className="h-4 w-4" />
            {t("exportCsv" as never)}
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={periods.length === 0}
            onClick={() => void downloadMoneyXlsx({ invoices: periods, receipts, lang })}
          >
            <FileSpreadsheet className="h-4 w-4" />
            {t("exportExcel" as never)}
          </Button>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <SummaryCard label={t("balTotalInvoiced" as never)} value={totals.invoiced} />
        <SummaryCard
          label={t("balTotalPaid" as never)}
          value={totals.paid}
          accent="text-emerald-600 dark:text-emerald-400"
        />
        <SummaryCard
          label={t("balTotalOutstanding" as never)}
          value={totals.outstanding}
          accent="text-amber-600 dark:text-amber-400"
        />
        <SummaryCard
          label={t("balOverdue" as never)}
          value={totals.overdue}
          plain
          accent={totals.overdue > 0 ? "text-rose-600 dark:text-rose-400" : ""}
        />
      </div>

      <Card>
        <CardContent className="pt-6">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[760px] text-base" dir={lang === "ar" ? "rtl" : "ltr"}>
              <thead>
                <tr className="border-b border-border text-start text-muted-foreground">
                  <th className="px-2 py-2.5 text-start font-medium">{t("colMerchant" as never)}</th>
                  <th className="px-2 py-2.5 text-end font-medium">{t("colInvoiced" as never)}</th>
                  <th className="px-2 py-2.5 text-end font-medium">{t("colPaid" as never)}</th>
                  <th className="px-2 py-2.5 text-end font-medium">{t("colOutstanding" as never)}</th>
                  <th className="px-2 py-2.5 text-end font-medium">{t("colOverdueCount" as never)}</th>
                </tr>
              </thead>
              <tbody>
                {balances.map((row) => (
                  <tr key={row.key} className="border-b border-border/60">
                    <td className="px-2 py-2.5">
                      <div className="font-medium">{row.merchantName}</div>
                      {row.tenantId ? (
                        <div className="font-mono text-xs text-muted-foreground">
                          {formatTenantRef(row.tenantId, lang)}
                        </div>
                      ) : null}
                    </td>
                    <td className="px-2 py-2.5 text-end tabular-nums">
                      <MoneyAmount amount={row.invoiced} />
                    </td>
                    <td className="px-2 py-2.5 text-end tabular-nums text-emerald-600 dark:text-emerald-400">
                      <MoneyAmount amount={row.paid} />
                    </td>
                    <td className="px-2 py-2.5 text-end tabular-nums font-medium text-amber-600 dark:text-amber-400">
                      <MoneyAmount amount={row.outstanding} />
                    </td>
                    <td className="px-2 py-2.5 text-end tabular-nums">
                      {row.overdueCount > 0 ? (
                        <span className="rounded-full bg-rose-500/15 px-2 py-0.5 text-sm font-medium text-rose-700 dark:text-rose-300">
                          {row.overdueCount}
                        </span>
                      ) : (
                        "0"
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
