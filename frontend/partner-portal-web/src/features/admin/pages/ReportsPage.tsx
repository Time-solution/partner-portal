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
  vatControl,
  type ReportEntry,
} from "@/lib/reports/settlementReports";
import { scopeReportEntries } from "@/lib/reports/reportScope";
import { buildContributionStatement } from "@/lib/reports/contributionStatement";
import { describeRange, filterByDate } from "@/lib/filters/dateRange";
import {
  accountVisible,
  canSeeContribution,
  financeSideScope,
  marginVisible,
  netVatVisible,
  purchaseVisible,
  salesVisible,
} from "@/lib/filters/financeSide";
import { contributionProformaFromStatement } from "@/lib/invoice/proformaSources";
import { downloadProformaPdf } from "@/lib/invoice/proformaPdf";
import { downloadProformaXlsx } from "@/lib/invoice/proformaExcel";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { useDateRange } from "../hooks/useDateRange";
import { useFinanceSide } from "../hooks/useFinanceSide";
import { DateRangeFilter } from "../components/DateRangeFilter";
import { PurchaseSalesFilter } from "../components/PurchaseSalesFilter";

type EntitySel = { value: string; kind: "all" | "partner" | "merchant"; id?: string; label: string };

export function ReportsPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const [data, setData] = useState<PortalData | null>(null);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [params, setParams] = useSearchParams();
  const { range, setRange } = useDateRange();
  const { side, setSide } = useFinanceSide();
  const { role } = usePortalSession();
  const sideScope = useMemo(() => financeSideScope(role), [role]);

  useEffect(() => {
    void getPortalDataSource()
      .getAll()
      .then(setData)
      .finally(() => setLoading(false));
  }, []);

  const entries = useMemo(() => (data ? toReportEntries(data) : []), [data]);
  const periods = useMemo(() => listPeriods(entries), [entries]);
  const fallbackPeriod = periods[0] ?? "";

  // SALES / PURCHASE / settlement = TRUE day-to-day (source arrays narrowed BEFORE toReportEntries);
  // VAT = period/month-overlap (never day-sliced). settlementReports money math is UNCHANGED.
  const { effectiveRange, entries: dayEntries, vatEntries, dayGrain } = useMemo(
    () =>
      data
        ? scopeReportEntries(data, range, fallbackPeriod)
        : { effectiveRange: range, entries: [], vatEntries: [], dayGrain: false },
    [data, range, fallbackPeriod],
  );
  // Display tag for the active range (month, day, or "from → to") — aggregates across months.
  const rangeLabel = describeRange(range, fallbackPeriod);
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

  // Entity scope (partner/merchant) layers ON TOP of the date grain — it can only narrow, never widen.
  const entityScope = (list: ReportEntry[]): ReportEntry[] => {
    if (!selected || selected.kind === "all") return list;
    if (selected.kind === "partner") return list.filter((e) => e.partnerId === selected.id);
    return list.filter((e) => e.tenantId === selected.id);
  };

  // Day-to-day scoped entries (single source — pure slice; multi-month ranges aggregate every case).
  const scopedEntries = useMemo(() => entityScope(dayEntries), [dayEntries, selected]);

  const tb = useMemo(() => trialBalance(scopedEntries, "all"), [scopedEntries]);
  const platform = useMemo(() => platformTotals(scopedEntries, "all"), [scopedEntries]);
  // Net VAT stays a period/filing figure — built from the month-overlap entries, never day-sliced.
  const vat = useMemo(() => vatControl(entityScope(vatEntries)), [vatEntries, selected]);
  const group = useMemo(() => orderGroup(scopedEntries, ""), [scopedEntries]);

  // Platform Contribution / P&L — sales (day-to-day) − cost of sales (day-to-day) = gross; − net VAT
  // (period) = net. Reads existing read models; no recompute. Management both-sides figure: gated to
  // PlatformAdmin / Accountant AND shown only in the "Both" view (a single-side slice can't net it).
  const contribution = useMemo(
    () => buildContributionStatement(scopedEntries, entityScope(vatEntries)),
    [scopedEntries, vatEntries, selected],
  );
  const showContribution = canSeeContribution(sideScope) && side === "both";

  const doExportContribution = async (kind: "pdf" | "xlsx") => {
    const doc = contributionProformaFromStatement(contribution, rangeLabel, lang);
    if (kind === "pdf") await downloadProformaPdf(doc);
    else await downloadProformaXlsx(doc);
  };

  // Purchase/Sales narrows which account rows show (and re-totals the visible subset for display —
  // not money math, just summing already-derived balances). "Both" === current full view.
  const tbAccounts = useMemo(
    () => tb.accounts.filter((a) => accountVisible(a.account, side, sideScope)),
    [tb.accounts, side, sideScope],
  );
  const tbDebits = useMemo(() => sumCol(tbAccounts, "debit"), [tbAccounts]);
  const tbCredits = useMemo(() => sumCol(tbAccounts, "credit"), [tbAccounts]);
  const groupAccounts = useMemo(
    () => (group ? group.accounts.filter((a) => accountVisible(a.account, side, sideScope)) : []),
    [group, side, sideScope],
  );

  const update = (key: "entity", value: string) => {
    const next = new URLSearchParams(params);
    next.set(key, value);
    setParams(next, { replace: true });
  };

  // Export — pre-filtered to the selected entity + period (reuses the settlement export).
  const exportInput: SettlementExportInput | null = useMemo(() => {
    if (!data) return null;
    // Day-precise export: cases dated within the active range (matches the on-screen sales/purchase).
    let cases = filterByDate(data.settlementCases, effectiveRange, (c) => c.createdAt);
    if (selected?.kind === "partner") cases = cases.filter((c) => c.partnerId === selected.id);
    if (selected?.kind === "merchant") cases = cases.filter((c) => c.tenantId === selected.id);
    const nameByTenant = new Map(data.activations.map((a) => [a.tenantId, a.merchantName]));
    return {
      cases,
      merchantName: (tid?: string) => (tid && nameByTenant.get(tid)) || tid || "—",
      lang,
    };
  }, [data, effectiveRange, selected, lang]);

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
            <DateRangeFilter range={range} onChange={setRange} lang={lang} />
            <label className="flex flex-col gap-1 text-sm">
              <span className="font-medium text-muted-foreground">{t("sideFilterLabel" as never)}</span>
              <PurchaseSalesFilter side={side} onChange={setSide} lang={lang} />
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

      {/* Subscription fees are month-keyed — a day range can only month-overlap them (not day-split). */}
      {dayGrain ? (
        <div
          className="rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-xs text-amber-950 dark:text-amber-100"
          dir={lang === "ar" ? "rtl" : "ltr"}
        >
          {t("reportsSubscriptionMonthNote" as never)}
        </div>
      ) : null}

      {/* Platform totals — gated by side + role scope (margin is platform-only) */}
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {marginVisible(side, sideScope) ? (
          <TotalCard label={t("reportsResaleMargin" as never)} value={platform.resaleMargin} />
        ) : null}
        {salesVisible(side, sideScope) ? (
          <TotalCard label={t("reportsFeeRevenue" as never)} value={platform.feeRevenue} />
        ) : null}
        {netVatVisible(side) ? (
          <TotalCard
            label={t("reportsNetVat" as never)}
            value={vat.netVatToZatca}
            note={`${t("reportsNetVatPeriodNote" as never)} · ${rangeLabel}`}
            beta={<BetaBadge lang={lang} />}
          />
        ) : null}
        <TotalCard label={t("reportsReflectionCount" as never)} value={platform.reflectionCount} plain />
      </div>

      {/* Platform Contribution / P&L (BETA) — management only (PlatformAdmin / Accountant) */}
      {showContribution ? (
        <Card dir={lang === "ar" ? "rtl" : "ltr"}>
          <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3">
            <div>
              <CardTitle className="flex items-center gap-2 text-lg">
                {t("contributionTitle" as never)}
                <BetaBadge lang={lang} />
              </CardTitle>
              <CardDescription>{t("contributionDesc" as never)}</CardDescription>
            </div>
            <div className="flex flex-wrap gap-2 print:hidden">
              <Button size="sm" variant="outline" onClick={() => void doExportContribution("pdf")}>
                <FileDown className="h-4 w-4" />
                {t("statementExportPdf" as never)}
              </Button>
              <Button size="sm" variant="outline" onClick={() => void doExportContribution("xlsx")}>
                <FileSpreadsheet className="h-4 w-4" />
                {t("statementExportExcel" as never)}
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            <table className="w-full text-sm">
              <tbody>
                <PnlRow label={t("contributionResale" as never)} value={contribution.salesResale} />
                <PnlRow label={t("contributionFee" as never)} value={contribution.salesFee} />
                <PnlRow label={t("contributionSalesTotal" as never)} value={contribution.salesTotal} strong />
                <PnlRow
                  label={t("contributionCostOfSales" as never)}
                  value={contribution.costOfSales}
                  negative
                />
                <PnlRow
                  label={t("contributionGross" as never)}
                  value={contribution.grossContribution}
                  strong
                  rule
                />
                <PnlRow
                  label={t("contributionNetVat" as never)}
                  value={contribution.netVatToZatca}
                  negative
                  note={`${t("reportsNetVatPeriodNote" as never)} · ${rangeLabel}`}
                />
                <PnlRow
                  label={t("contributionNet" as never)}
                  value={contribution.netContribution}
                  strong
                  rule
                  emphasize
                />
              </tbody>
            </table>
            {dayGrain ? (
              <p className="mt-3 text-xs text-muted-foreground">{t("contributionGrainNote" as never)}</p>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

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
              {tbAccounts.length === 0 ? (
                <tr>
                  <td colSpan={3} className="py-6 text-center text-muted-foreground">
                    {t("reportsEmpty" as never)}
                  </td>
                </tr>
              ) : (
                tbAccounts.map((a) => (
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
                <td className="py-2 text-end tabular-nums"><MoneyAmount amount={tbDebits} /></td>
                <td className="py-2 text-end tabular-nums"><MoneyAmount amount={tbCredits} /></td>
              </tr>
            </tfoot>
          </table>
        </CardContent>
      </Card>

      {/* Per-partner (purchase) + per-merchant (sales) statements — gated by side + role scope */}
      <div className="grid gap-6 xl:grid-cols-2">
        {purchaseVisible(side, sideScope) ? (
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
                const s = partnerStatement(scopedEntries, p.partnerId, "all");
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
        ) : null}

        {salesVisible(side, sideScope) ? (
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
                const s = merchantStatement(scopedEntries, m.tenantId, "all");
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
        ) : null}
      </div>

      {/* Order-group totals for the period */}
      {group ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">{t("reportsOrderGroup" as never)}</CardTitle>
            <CardDescription>
              {rangeLabel} · {t("reportsOrders" as never)}: {group.orderCount}
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
                {groupAccounts.map((a) => (
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
                  <td className="py-2 text-end tabular-nums"><MoneyAmount amount={sumCol(groupAccounts, "debit")} /></td>
                  <td className="py-2 text-end tabular-nums"><MoneyAmount amount={sumCol(groupAccounts, "credit")} /></td>
                </tr>
              </tfoot>
            </table>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}

/** Display-only subtotal of the VISIBLE account rows (sums already-derived balances; no money math). */
function sumCol(rows: { debit: number; credit: number }[], col: "debit" | "credit"): number {
  return Math.round(rows.reduce((s, r) => s + r[col], 0) * 100) / 100;
}

/** One P&L line. `negative` shows the figure as a subtraction; `strong`/`emphasize` weight subtotals. */
function PnlRow({
  label,
  value,
  negative,
  strong,
  emphasize,
  rule,
  note,
}: {
  label: string;
  value: number;
  negative?: boolean;
  strong?: boolean;
  emphasize?: boolean;
  rule?: boolean;
  note?: string;
}) {
  return (
    <tr
      className={`${rule ? "border-t border-border" : "border-b border-border/40"} ${
        emphasize ? "bg-primary/5" : ""
      }`}
    >
      <td className={`py-2 ${strong ? "font-semibold" : ""}`}>
        {negative ? "− " : ""}
        {label}
        {note ? <span className="block text-xs font-normal text-muted-foreground">{note}</span> : null}
      </td>
      <td
        className={`py-2 text-end tabular-nums ${strong ? "font-semibold" : ""} ${
          emphasize ? "text-base" : ""
        }`}
      >
        <MoneyAmount amount={value} />
      </td>
    </tr>
  );
}

function TotalCard({
  label,
  value,
  beta,
  plain,
  note,
}: {
  label: string;
  value: number;
  beta?: React.ReactNode;
  plain?: boolean;
  /** Small caption under the figure — e.g. "tax-period figure · 2026-06" for net VAT. */
  note?: string;
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
        {note ? <p className="mt-1 text-xs text-muted-foreground">{note}</p> : null}
      </CardContent>
    </Card>
  );
}
