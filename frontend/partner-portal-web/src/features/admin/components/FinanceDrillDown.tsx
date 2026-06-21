import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { ChevronLeft, ChevronRight, ExternalLink, Eye, FileSpreadsheet, FileText, Loader2, Printer } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { MoneyOrDash } from "@/components/MoneyOrDash";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import type { PortalData, SettlementCase } from "@/lib/data/types";
import { settlementAccountLabel } from "@/lib/i18n/domainLabels";
import { formatOrderRef } from "@/lib/format/recordId";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { drillContextText, drillScopeLabel } from "@/lib/ui/drillContext";
import { useTranslator, type Lang } from "@/lib/i18n";
import {
  aggregateAccounts,
  listPeriods,
  merchantStatement,
  merchantsIn,
  partnerStatement,
  partnersIn,
  platformTotals,
  toReportEntries,
  type ReportEntry,
} from "@/lib/reports/settlementReports";
import { statementProformaFromPartner } from "@/lib/invoice/proformaSources";
import { downloadProformaPdf } from "@/lib/invoice/proformaPdf";
import { downloadProformaXlsx } from "@/lib/invoice/proformaExcel";
import { JournalTable } from "./JournalTable";

type Company =
  | { kind: "partner"; id: string; name: string }
  | { kind: "merchant"; id: string; name: string };

const sar = (n: number) => <MoneyAmount amount={n} />;

/**
 * Four-level finance drill-down — DISPLAY ONLY, scope-aware (reads getAll() which is
 * already filtered to the viewer's org). L1 per-company headline totals → L2 statement
 * → L3 order detail (journal DR=CR + four-way reconciliation). The same single-source
 * numbers a partner sees here tie exactly to the accountant's view of that partner.
 */
export function FinanceDrillDown({
  lang,
  canOpenReports = false,
}: {
  lang: Lang;
  /** Show "Open in Reports" deep-link (only for roles that can reach /finance). */
  canOpenReports?: boolean;
}) {
  const t = useTranslator(lang);
  const navigate = useNavigate();
  const { org } = usePortalSession();
  const [data, setData] = useState<PortalData | null>(null);
  const [loading, setLoading] = useState(true);
  const [company, setCompany] = useState<Company | null>(null);
  const [orderRef, setOrderRef] = useState<string | null>(null);

  useEffect(() => {
    void getPortalDataSource()
      .getAll()
      .then(setData)
      .finally(() => setLoading(false));
  }, []);

  const entries = useMemo(() => (data ? toReportEntries(data) : []), [data]);
  const periods = useMemo(() => listPeriods(entries), [entries]);
  const period = periods[0] || "";
  const Back = lang === "ar" ? ChevronRight : ChevronLeft;
  const Fwd = lang === "ar" ? ChevronLeft : ChevronRight;

  const periodEntries = useMemo(
    () => entries.filter((e) => !period || e.period === period),
    [entries, period],
  );

  const openReports = (c: Company) => {
    const entity = `${c.kind}:${c.id}`;
    navigate(`/finance/reports?entity=${encodeURIComponent(entity)}&period=${encodeURIComponent(period)}`);
  };

  if (loading || !data) {
    return (
      <div className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  // -------- L3: order detail --------
  if (company && orderRef) {
    const c = data.settlementCases.find((x) => x.externalTransactionId === orderRef);
    return (
      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
          <div>
            <CardTitle className="flex items-center gap-2 text-lg">
              {formatOrderRef(orderRef, lang)}
              <BetaBadge lang={lang} />
            </CardTitle>
            <CardDescription>{company.name}</CardDescription>
          </div>
          <Button size="sm" variant="ghost" onClick={() => setOrderRef(null)}>
            <Back className="h-4 w-4" />
            {t("drillBack" as never)}
          </Button>
        </CardHeader>
        <CardContent className="space-y-4">
          {c?.collection ? <Reconciliation case_={c} lang={lang} /> : null}
          {c ? (
            <JournalTable lang={lang} lines={c.journal.lines} caption={t("settlementJournalCaption" as never)} />
          ) : (
            <OrderJournalFromEntries entries={periodEntries} orderRef={orderRef} lang={lang} />
          )}
        </CardContent>
      </Card>
    );
  }

  // -------- L2: company statement --------
  if (company) {
    const own = periodEntries.filter((e) =>
      company.kind === "partner" ? e.partnerId === company.id : e.tenantId === company.id,
    );
    const accounts = aggregateAccounts(own);
    const orderRefs = Array.from(new Set(own.filter((e) => e.financial).map((e) => e.orderRef)));
    const totals =
      company.kind === "partner"
        ? partnerStatement(periodEntries, company.id, period)
        : merchantStatement(periodEntries, company.id, period);
    // Direction 2 — Zahy-owes-partner settlement statement (a SEPARATE proforma document).
    const partnerStmt = company.kind === "partner" ? partnerStatement(periodEntries, company.id, period) : null;
    return (
      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
          <div>
            <CardTitle className="text-lg">{company.name}</CardTitle>
            <CardDescription>
              {period} · {t("reportsOrders" as never)}: {totals.orderCount}
            </CardDescription>
          </div>
          <div className="flex flex-wrap gap-2">
            {partnerStmt ? (
              <>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => void downloadProformaPdf(statementProformaFromPartner(partnerStmt, lang))}
                >
                  <FileText className="h-4 w-4" />
                  {lang === "ar" ? "بيان PDF" : "Statement PDF"}
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => void downloadProformaXlsx(statementProformaFromPartner(partnerStmt, lang))}
                >
                  <FileSpreadsheet className="h-4 w-4" />
                  {lang === "ar" ? "بيان Excel" : "Statement Excel"}
                </Button>
              </>
            ) : null}
            {canOpenReports ? (
              <Button size="sm" variant="outline" onClick={() => openReports(company)}>
                <ExternalLink className="h-4 w-4" />
                {t("drillOpenInReports" as never)}
              </Button>
            ) : (
              <Button size="sm" variant="outline" onClick={() => window.print()}>
                <Printer className="h-4 w-4" />
                {t("reportsPrint" as never)}
              </Button>
            )}
            <Button size="sm" variant="ghost" onClick={() => setCompany(null)}>
              <Back className="h-4 w-4" />
              {t("drillBack" as never)}
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* per-account balances */}
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border text-muted-foreground">
                <th className="py-2 text-start font-medium">{t("journalColAccount" as never)}</th>
                <th className="py-2 text-end font-medium">{t("journalColDebit" as never)}</th>
                <th className="py-2 text-end font-medium">{t("journalColCredit" as never)}</th>
              </tr>
            </thead>
            <tbody>
              {accounts.map((a) => (
                <tr key={a.account} className="border-b border-border/60">
                  <td className="py-2">{settlementAccountLabel(lang, a.account)}</td>
                  <td className="py-2 text-end tabular-nums"><MoneyOrDash amount={a.debit || undefined} /></td>
                  <td className="py-2 text-end tabular-nums"><MoneyOrDash amount={a.credit || undefined} /></td>
                </tr>
              ))}
            </tbody>
          </table>
          {/* orders → drill to L3 */}
          <div className="space-y-1.5">
            {orderRefs.map((ref) => (
              <button
                key={ref}
                className="flex w-full items-center justify-between gap-2 rounded-md border border-border px-3 py-2 text-sm hover:bg-muted/50"
                onClick={() => setOrderRef(ref)}
              >
                <span className="tabular-nums" title={ref}>
                  {formatOrderRef(ref, lang)}
                </span>
                <span className="flex items-center gap-1 text-muted-foreground">
                  {t("drillViewDetail" as never)}
                  <Fwd className="h-4 w-4" />
                </span>
              </button>
            ))}
          </div>
        </CardContent>
      </Card>
    );
  }

  // -------- L1: summary cards --------
  const platform = platformTotals(periodEntries, period || "all");
  const partners = partnersIn(periodEntries);
  const merchants = merchantsIn(periodEntries);

  // DISPLAY-ONLY context header: the same drill-down renders on several dashboards, so
  // show WHOSE (already-scoped) data is on screen + which period. Scope is not changed here.
  const scopeName = drillScopeLabel(org, t("drillScopePlatform" as never));

  return (
    <div className="space-y-3">
      <div
        className="flex flex-wrap items-center gap-2 rounded-md border border-border bg-muted/40 px-3 py-2 text-sm"
        dir={lang === "ar" ? "rtl" : "ltr"}
      >
        <Eye className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
        <span className="text-muted-foreground">{t("drillContextLabel" as never)}:</span>
        <span className="font-semibold text-foreground">
          {drillContextText(scopeName, period, t("drillAllPeriods" as never))}
        </span>
      </div>
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            {t("drillSummaryTitle" as never)}
            <BetaBadge lang={lang} />
          </CardTitle>
        <CardDescription>
          {period} · {t("reportsResaleMargin" as never)}: {sar(platform.resaleMargin)} ·{" "}
          {t("reportsNetVat" as never)}: {sar(platform.netVatToZatca)}
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {partners.map((p) => {
          const s = partnerStatement(periodEntries, p.partnerId, period);
          return (
            <CompanyCard
              key={`p-${p.partnerId}`}
              title={s.partnerName || p.partnerName}
              subtitle={`${t("reportsPayable" as never)}`}
              value={sar(s.payable)}
              meta={`${t("reportsOrders" as never)}: ${s.orderCount}`}
              label={t("drillViewStatement" as never)}
              Fwd={Fwd}
              onClick={() => setCompany({ kind: "partner", id: p.partnerId, name: s.partnerName || p.partnerName })}
            />
          );
        })}
        {merchants.map((m) => {
          const s = merchantStatement(periodEntries, m.tenantId, period);
          return (
            <CompanyCard
              key={`m-${m.tenantId}`}
              title={s.merchantName || m.merchantName}
              subtitle={`${t("reportsReceivable" as never)}`}
              value={sar(s.receivable)}
              meta={`${t("reportsOrders" as never)}: ${s.orderCount}`}
              label={t("drillViewStatement" as never)}
              Fwd={Fwd}
              onClick={() => setCompany({ kind: "merchant", id: m.tenantId, name: s.merchantName || m.merchantName })}
            />
          );
        })}
        </CardContent>
      </Card>
    </div>
  );
}

function CompanyCard({
  title,
  subtitle,
  value,
  meta,
  label,
  Fwd,
  onClick,
}: {
  title: string;
  subtitle: string;
  value: ReactNode;
  meta: string;
  label: string;
  Fwd: typeof ChevronRight;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className="flex flex-col gap-1 rounded-md border border-border p-3 text-start transition-colors hover:border-primary/60 hover:bg-muted/40"
    >
      <span className="font-semibold">{title}</span>
      <span className="text-xs text-muted-foreground">{subtitle}</span>
      <span className="text-xl font-bold tabular-nums">{value}</span>
      <span className="mt-1 flex items-center justify-between text-xs text-muted-foreground">
        {meta}
        <span className="flex items-center gap-0.5 text-primary">
          {label}
          <Fwd className="h-3.5 w-3.5" />
        </span>
      </span>
    </button>
  );
}

function Reconciliation({ case_, lang }: { case_: SettlementCase; lang: Lang }) {
  const t = useTranslator(lang);
  const s = case_.collection!.split;
  const total = case_.collection!.totalCollected.amount;
  return (
    <div className="rounded-md bg-muted/40 p-3 text-sm">
      <p className="mb-1 font-medium">{t("drillReconciliation" as never)}</p>
      <p className="flex flex-wrap items-baseline gap-1 tabular-nums">
        <MoneyAmount amount={total} />
        <span>=</span>
        <MoneyAmount amount={s.merchant.amount} />
        <span>+</span>
        <MoneyAmount amount={s.deliveryCompany.amount} />
        <span>+</span>
        <MoneyAmount amount={s.zahy.amount} />
        <span>+</span>
        <MoneyAmount amount={s.zatca.amount} />
      </p>
    </div>
  );
}

function OrderJournalFromEntries({
  entries,
  orderRef,
  lang,
}: {
  entries: ReportEntry[];
  orderRef: string;
  lang: Lang;
}) {
  const t = useTranslator(lang);
  const entry = entries.find((e) => e.orderRef === orderRef && e.financial);
  if (!entry) return <p className="text-sm text-muted-foreground">{t("reportsEmpty" as never)}</p>;
  return <JournalTable lang={lang} lines={entry.lines} caption={t("settlementJournalCaption" as never)} />;
}
