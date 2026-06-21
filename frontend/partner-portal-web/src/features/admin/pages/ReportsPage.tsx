import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { FileDown, FileSpreadsheet, Loader2, Printer } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { MoneyOrDash } from "@/components/MoneyOrDash";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import type { PortalData } from "@/lib/data/types";
import { useTranslator, type Lang } from "@/lib/i18n";
import { settlementAccountLabel } from "@/lib/i18n/domainLabels";
import {
  downloadSettlementCsv,
  downloadSettlementXlsx,
  type SettlementExportInput,
} from "@/lib/export/settlementExport";
import {
  listPeriods,
  merchantStatement,
  merchantsIn,
  orderGroup,
  partnerStatement,
  partnersIn,
  platformTotals,
  toReportEntries,
  trialBalance,
} from "@/lib/reports/settlementReports";

type EntitySel = { value: string; kind: "all" | "partner" | "merchant"; id?: string; label: string };

export function ReportsPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const [data, setData] = useState<PortalData | null>(null);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [params, setParams] = useSearchParams();

  useEffect(() => {
    void getPortalDataSource()
      .getAll()
      .then(setData)
      .finally(() => setLoading(false));
  }, []);

  const entries = useMemo(() => (data ? toReportEntries(data) : []), [data]);
  const periods = useMemo(() => listPeriods(entries), [entries]);

  const period = params.get("period") || periods[0] || "";
  const entityValue = params.get("entity") || "all";

  const entityOptions: EntitySel[] = useMemo(() => {
    const opts: EntitySel[] = [{ value: "all", kind: "all", label: t("reportsEntityAll" as never) }];
    for (const p of partnersIn(entries)) {
      opts.push({ value: `partner:${p.partnerId}`, kind: "partner", id: p.partnerId, label: p.partnerName || p.partnerId });
    }
    for (const m of merchantsIn(entries)) {
      opts.push({ value: `merchant:${m.tenantId}`, kind: "merchant", id: m.tenantId, label: m.merchantName || m.tenantId });
    }
    return opts;
  }, [entries, t]);

  const selected = entityOptions.find((o) => o.value === entityValue) ?? entityOptions[0];

  // The scoped entries for the selected period (single source — pure slice).
  const periodEntries = useMemo(
    () => entries.filter((e) => !period || e.period === period),
    [entries, period],
  );
  const scopedEntries = useMemo(() => {
    if (!selected || selected.kind === "all") return periodEntries;
    if (selected.kind === "partner") return periodEntries.filter((e) => e.partnerId === selected.id);
    return periodEntries.filter((e) => e.tenantId === selected.id);
  }, [periodEntries, selected]);

  const tb = useMemo(() => trialBalance(scopedEntries, period || "all"), [scopedEntries, period]);
  const platform = useMemo(() => platformTotals(scopedEntries, period || "all"), [scopedEntries, period]);
  const group = useMemo(() => (period ? orderGroup(entries, period) : null), [entries, period]);

  const update = (key: "period" | "entity", value: string) => {
    const next = new URLSearchParams(params);
    next.set(key, value);
    setParams(next, { replace: true });
  };

  // Export — pre-filtered to the selected entity + period (reuses the settlement export).
  const exportInput: SettlementExportInput | null = useMemo(() => {
    if (!data) return null;
    let cases = data.settlementCases.filter((c) => !period || c.createdAt.slice(0, 7) === period);
    if (selected?.kind === "partner") cases = cases.filter((c) => c.partnerId === selected.id);
    if (selected?.kind === "merchant") cases = cases.filter((c) => c.tenantId === selected.id);
    const nameByTenant = new Map(data.activations.map((a) => [a.tenantId, a.merchantName]));
    return {
      cases,
      merchantName: (tid?: string) => (tid && nameByTenant.get(tid)) || tid || "—",
      lang,
    };
  }, [data, period, selected, lang]);

  const doExportXlsx = async () => {
    if (!exportInput) return;
    setExporting(true);
    try {
      await downloadSettlementXlsx(exportInput);
    } finally {
      setExporting(false);
    }
  };

  if (loading || !data) {
    return (
      <div className="flex items-center gap-2 py-8 text-base text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Filters + actions */}
      <Card className="print:hidden">
        <CardHeader className="flex flex-row flex-wrap items-end justify-between gap-3">
          <div className="flex flex-wrap items-end gap-3">
            <label className="flex flex-col gap-1 text-sm">
              <span className="font-medium text-muted-foreground">{t("reportsPeriod" as never)}</span>
              <select
                className="h-9 rounded-md border border-input bg-background px-3 text-sm"
                value={period}
                onChange={(e) => update("period", e.target.value)}
              >
                {periods.length === 0 ? <option value="">—</option> : null}
                {periods.map((p) => (
                  <option key={p} value={p}>
                    {p}
                  </option>
                ))}
              </select>
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span className="font-medium text-muted-foreground">{t("reportsEntity" as never)}</span>
              <select
                className="h-9 min-w-44 rounded-md border border-input bg-background px-3 text-sm"
                value={entityValue}
                onChange={(e) => update("entity", e.target.value)}
              >
                {entityOptions.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button size="sm" variant="outline" disabled={exporting || !exportInput} onClick={() => void doExportXlsx()}>
              {exporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <FileSpreadsheet className="h-4 w-4" />}
              {t("exportExcel" as never)}
            </Button>
            <Button
              size="sm"
              variant="outline"
              disabled={!exportInput}
              onClick={() => exportInput && downloadSettlementCsv(exportInput)}
            >
              <FileDown className="h-4 w-4" />
              {t("exportCsv" as never)}
            </Button>
            <Button size="sm" variant="outline" onClick={() => window.print()}>
              <Printer className="h-4 w-4" />
              {t("reportsPrint" as never)}
            </Button>
          </div>
        </CardHeader>
      </Card>

      {/* Platform totals */}
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <TotalCard label={t("reportsResaleMargin" as never)} value={platform.resaleMargin} />
        <TotalCard label={t("reportsFeeRevenue" as never)} value={platform.feeRevenue} />
        <TotalCard
          label={t("reportsNetVat" as never)}
          value={platform.netVatToZatca}
          beta={<BetaBadge lang={lang} />}
        />
        <TotalCard label={t("reportsReflectionCount" as never)} value={platform.reflectionCount} plain />
      </div>

      {/* Trial balance */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between gap-2 text-lg">
            {t("reportsTrialBalance" as never)}
            <span
              className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                tb.balanced
                  ? "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300"
                  : "bg-red-500/15 text-red-700 dark:text-red-300"
              }`}
            >
              {tb.balanced ? t("journalBalanced" as never) : t("journalUnbalanced" as never)}
            </span>
          </CardTitle>
          <CardDescription>{t("reportsTrialBalanceDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border text-muted-foreground">
                <th className="py-2 text-start font-medium">{t("journalColAccount" as never)}</th>
                <th className="py-2 text-end font-medium">{t("journalColDebit" as never)}</th>
                <th className="py-2 text-end font-medium">{t("journalColCredit" as never)}</th>
              </tr>
            </thead>
            <tbody>
              {tb.accounts.length === 0 ? (
                <tr>
                  <td colSpan={3} className="py-6 text-center text-muted-foreground">
                    {t("reportsEmpty" as never)}
                  </td>
                </tr>
              ) : (
                tb.accounts.map((a) => (
                  <tr key={a.account} className="border-b border-border/60">
                    <td className="py-2">{settlementAccountLabel(lang, a.account)}</td>
                    <td className="py-2 text-end tabular-nums"><MoneyOrDash amount={a.debit || undefined} /></td>
                    <td className="py-2 text-end tabular-nums"><MoneyOrDash amount={a.credit || undefined} /></td>
                  </tr>
                ))
              )}
            </tbody>
            <tfoot>
              <tr className="font-semibold">
                <td className="py-2">{t("journalTotals" as never)}</td>
                <td className="py-2 text-end tabular-nums"><MoneyAmount amount={tb.totalDebits} /></td>
                <td className="py-2 text-end tabular-nums"><MoneyAmount amount={tb.totalCredits} /></td>
              </tr>
            </tfoot>
          </table>
        </CardContent>
      </Card>

      {/* Per-partner + per-merchant statements */}
      <div className="grid gap-6 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">{t("reportsPartnerStatements" as never)}</CardTitle>
            <CardDescription>{t("reportsPartnerStatementsDesc" as never)}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {partnersIn(scopedEntries).length === 0 ? (
              <p className="text-sm text-muted-foreground">{t("reportsEmpty" as never)}</p>
            ) : (
              partnersIn(scopedEntries).map((p) => {
                const s = partnerStatement(scopedEntries, p.partnerId, period || "all");
                return (
                  <div key={p.partnerId} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border p-3">
                    <div>
                      <p className="font-medium">{s.partnerName || p.partnerName}</p>
                      <p className="text-sm text-muted-foreground">
                        {t("reportsOrders" as never)}: {s.orderCount}
                        {s.reflectionCount ? ` · ${t("reportsReflections" as never)}: ${s.reflectionCount}` : ""}
                      </p>
                    </div>
                    <div className="text-end">
                      <p className="text-xs text-muted-foreground">{t("reportsPayable" as never)}</p>
                      <p className="font-semibold tabular-nums">
                        <MoneyAmount amount={s.payable} />
                      </p>
                    </div>
                  </div>
                );
              })
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-lg">{t("reportsMerchantStatements" as never)}</CardTitle>
            <CardDescription>{t("reportsMerchantStatementsDesc" as never)}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {merchantsIn(scopedEntries).length === 0 ? (
              <p className="text-sm text-muted-foreground">{t("reportsEmpty" as never)}</p>
            ) : (
              merchantsIn(scopedEntries).map((m) => {
                const s = merchantStatement(scopedEntries, m.tenantId, period || "all");
                return (
                  <div key={m.tenantId} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border p-3">
                    <div>
                      <p className="font-medium">{s.merchantName || m.merchantName}</p>
                      <p className="text-sm text-muted-foreground">
                        {t("reportsOrders" as never)}: {s.orderCount}
                        {s.fees ? (
                          <>
                            {" · "}
                            {t("reportsFees" as never)}: <MoneyAmount amount={s.fees} />
                          </>
                        ) : (
                          ""
                        )}
                      </p>
                    </div>
                    <div className="text-end">
                      <p className="text-xs text-muted-foreground">{t("reportsReceivable" as never)}</p>
                      <p className="font-semibold tabular-nums">
                        <MoneyAmount amount={s.receivable} />
                      </p>
                    </div>
                  </div>
                );
              })
            )}
          </CardContent>
        </Card>
      </div>

      {/* Order-group totals for the period */}
      {group ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">{t("reportsOrderGroup" as never)}</CardTitle>
            <CardDescription>
              {period} · {t("reportsOrders" as never)}: {group.orderCount}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-muted-foreground">
                  <th className="py-2 text-start font-medium">{t("journalColAccount" as never)}</th>
                  <th className="py-2 text-end font-medium">{t("journalColDebit" as never)}</th>
                  <th className="py-2 text-end font-medium">{t("journalColCredit" as never)}</th>
                </tr>
              </thead>
              <tbody>
                {group.accounts.map((a) => (
                  <tr key={a.account} className="border-b border-border/60">
                    <td className="py-2">{settlementAccountLabel(lang, a.account)}</td>
                    <td className="py-2 text-end tabular-nums"><MoneyOrDash amount={a.debit || undefined} /></td>
                    <td className="py-2 text-end tabular-nums"><MoneyOrDash amount={a.credit || undefined} /></td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr className="font-semibold">
                  <td className="py-2">{t("journalTotals" as never)}</td>
                  <td className="py-2 text-end tabular-nums"><MoneyAmount amount={group.totalDebits} /></td>
                  <td className="py-2 text-end tabular-nums"><MoneyAmount amount={group.totalCredits} /></td>
                </tr>
              </tfoot>
            </table>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}

function TotalCard({
  label,
  value,
  beta,
  plain,
}: {
  label: string;
  value: number;
  beta?: React.ReactNode;
  plain?: boolean;
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="flex items-center gap-2 text-sm font-medium text-muted-foreground">
          {label}
          {beta}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-2xl font-bold tabular-nums">
          {plain ? value : <MoneyAmount amount={value} />}
        </p>
      </CardContent>
    </Card>
  );
}
