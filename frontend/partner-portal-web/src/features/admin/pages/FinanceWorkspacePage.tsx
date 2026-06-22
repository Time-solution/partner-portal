import { useCallback, useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Navigate, NavLink, Route, Routes, useLocation } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { MerchantActivationRow, PortalData, SubscriptionBillingPeriod } from "@/lib/data/types";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { platformTotals, toReportEntries, vatControl } from "@/lib/reports/settlementReports";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { FINANCE_WORKSPACE_TABS } from "@/lib/rbac/partnerModules";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { PageHeader } from "../components/PageHeader";
import { ModuleScreenRenderer } from "../moduleScreenRegistry";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { usePortalAnalytics } from "@/hooks/usePortalAnalytics";
import {
  BillingRevenueChart,
  PendingApprovalsChart,
  ReversalsImpactChart,
} from "../components/charts/FinanceCharts";
import { SettlementTrendChart } from "../components/charts/SettlementTrendChart";
import { billingPeriodStatusLabel } from "@/lib/i18n/domainLabels";
import { isPathActive, tabBarClass, tabLinkClass } from "@/lib/ui/tabs";
import { BalancesPage } from "./BalancesPage";
import { ReportsPage } from "./ReportsPage";
import { CommissionApprovalsPage, CommissionApprovalsNavLink } from "./CommissionApprovalsPage";
import { ManualInvoicePage } from "./ManualInvoicePage";
import { FinanceDrillDown } from "../components/FinanceDrillDown";

function FinanceTabNav({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const location = useLocation();

  return (
    <nav className={tabBarClass} aria-label={t("navFinance" as never)}>
      {FINANCE_WORKSPACE_TABS.map((tab) => {
        const to = `/finance/${tab.path}`;
        const isActive = isPathActive(location.pathname, to);
        return (
          <NavLink key={tab.id} to={to} className={tabLinkClass(isActive)}>
            {t(tab.labelKey as never)}
          </NavLink>
        );
      })}
    </nav>
  );
}

function FinanceOverview({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { analytics, loading } = usePortalAnalytics();
  const [data, setData] = useState<PortalData | null>(null);

  useEffect(() => {
    void getPortalDataSource().getAll().then(setData);
  }, []);

  if (loading || !analytics || !data) {
    return (
      <div className="flex items-center gap-2 text-base text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  // SINGLE SOURCE: every money figure reads settlementReports over the scoped dataset.
  // Net VAT = VatControlReport (2200 − 1300); fee revenue = 4200; resale margin = 4100 − 5100.
  // Never a flat 15% of a total.
  const entries = toReportEntries(data);
  const totals = platformTotals(entries, "all");
  const vat = vatControl(entries, "all");

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              {t("reportsResaleMargin" as never)}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold tabular-nums">
              <MoneyAmount amount={totals.resaleMargin} />
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              {t("reportsFeeRevenue" as never)}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold tabular-nums">
              <MoneyAmount amount={totals.feeRevenue} />
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm font-medium text-muted-foreground">
              {t("reportsNetVat" as never)}
              <BetaBadge lang={lang} />
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold tabular-nums">
              <MoneyAmount amount={vat.netVatToZatca} />
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              {t("financeKpiPending" as never)}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold tabular-nums">{analytics.pendingApprovals}</p>
          </CardContent>
        </Card>
      </div>

      <SettlementTrendChart
        lang={lang}
        data={analytics.settlementTrend}
        titleKey="financeChartSettlementTitle"
        descKey="financeChartSettlementDesc"
      />
      <div className="grid gap-6 xl:grid-cols-2">
        <BillingRevenueChart lang={lang} data={analytics.billingRevenue} />
        <PendingApprovalsChart lang={lang} count={analytics.pendingApprovals} />
      </div>
      <ReversalsImpactChart lang={lang} data={analytics.reversalImpact} />
      <FinanceDrillDown lang={lang} canOpenReports />
    </div>
  );
}

function FinanceVatSummary({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const [periods, setPeriods] = useState<SubscriptionBillingPeriod[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void getPortalDataSource()
      .getBillingPeriods()
      .then(setPeriods)
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="flex items-center gap-2 text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("financeVatTitle" as never)}</CardTitle>
        <CardDescription>{t("financeVatDesc" as never)}</CardDescription>
      </CardHeader>
      <CardContent>
        <ul className="divide-y divide-border text-sm">
          {periods.map((p) => (
            <li key={p.id} className="flex flex-wrap justify-between gap-2 py-3">
              <span className="font-medium">{p.merchantName}</span>
              <span>
                {t("billingVatLabel" as never)} <MoneyAmount amount={p.outputVat} /> ·{" "}
                {billingPeriodStatusLabel(lang, p.status)}
              </span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function FinancePendingApprovals({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { can } = usePortalSession();
  const [activations, setActivations] = useState<MerchantActivationRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const canSetTerms = can(PortalPermissions.Billing.SetTerms);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setActivations(await getPortalDataSource().getActivations());
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const pending = activations.filter(
    ({ workflow }) => workflow.stage === "PsmRequested" || workflow.stage === "AccountantTermsSet",
  );

  const setTerms = async (id: string) => {
    setBusyId(id);
    try {
      await getPortalDataSource().setActivationTerms(id, "Accountant (Finance)");
      await load();
    } finally {
      setBusyId(null);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center gap-2 text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("financePendingTitle" as never)}</CardTitle>
        <CardDescription>{t("financePendingDesc" as never)}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {pending.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("financePendingEmpty" as never)}</p>
        ) : (
          pending.map(({ activation: a, workflow: wf }) => (
            <div key={a.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border p-3">
              <div>
                <p className="font-medium">{a.merchantName}</p>
                <p className="text-sm text-muted-foreground">
                  {a.catalogItemName} · {wf.stage}
                </p>
              </div>
              {canSetTerms && wf.stage === "PsmRequested" ? (
                <Button
                  size="sm"
                  variant="outline"
                  disabled={busyId === a.id}
                  onClick={() => void setTerms(a.id)}
                >
                  {t("activationSetTerms" as never)}
                </Button>
              ) : null}
            </div>
          ))
        )}
        <CommissionApprovalsNavLink lang={lang} />
      </CardContent>
    </Card>
  );
}

export function FinanceWorkspacePage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("navFinance" as never)}
        description={t("financeWorkspaceDesc" as never)}
        lang={lang}
      />
      <FinanceTabNav lang={lang} />
      <Routes>
        <Route index element={<Navigate to="overview" replace />} />
        <Route path="overview" element={<FinanceOverview lang={lang} />} />
        <Route path="reports" element={<ReportsPage lang={lang} />} />
        <Route
          path="settlements"
          element={<ModuleScreenRenderer screenId="settlement" context={{ lang, financeMode: true }} />}
        />
        <Route
          path="reversals"
          element={<ModuleScreenRenderer screenId="reversals" context={{ lang, financeMode: true }} />}
        />
        <Route
          path="billing"
          element={<ModuleScreenRenderer screenId="billing" context={{ lang, financeMode: true }} />}
        />
        <Route path="balances" element={<BalancesPage lang={lang} />} />
        <Route path="vat" element={<FinanceVatSummary lang={lang} />} />
        <Route path="pending-approvals" element={<FinancePendingApprovals lang={lang} />} />
        <Route path="commission-approvals" element={<CommissionApprovalsPage lang={lang} />} />
        <Route path="invoices/new" element={<ManualInvoicePage lang={lang} />} />
        <Route path="*" element={<Navigate to="overview" replace />} />
      </Routes>
    </div>
  );
}
