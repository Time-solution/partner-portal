import { useCallback, useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Navigate, NavLink, Route, Routes, useLocation } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { MerchantActivationRow, SettlementCase, SubscriptionBillingPeriod } from "@/lib/data/types";
import { deriveSettlementSummary } from "@/lib/data/types";
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
import { billingPeriodStatusLabel, settlementStateLabel } from "@/lib/i18n/domainLabels";

function FinanceTabNav({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const location = useLocation();

  return (
    <nav
      className="flex flex-wrap gap-1 border-b border-border pb-2"
      aria-label={t("navFinance" as never)}
    >
      {FINANCE_WORKSPACE_TABS.map((tab) => {
        const to = `/finance/${tab.path}`;
        const isActive = location.pathname === to || location.pathname.startsWith(`${to}/`);
        return (
          <NavLink
            key={tab.id}
            to={to}
            className={[
              "rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
              isActive
                ? "bg-primary/10 text-primary"
                : "text-muted-foreground hover:bg-muted hover:text-foreground",
            ].join(" ")}
          >
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

  if (loading || !analytics) {
    return (
      <div className="flex items-center gap-2 text-base text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  const vatTotal = analytics.billingRevenue.reduce((sum, p) => sum + p.vatSar, 0);

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              {t("financeKpiSettlements" as never)}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold tabular-nums">{analytics.settlementCaseCount}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              {t("financeKpiReversals" as never)}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold tabular-nums">{analytics.reversalImpact.length}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              {t("financeKpiVat" as never)}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold tabular-nums">{vatTotal.toFixed(2)} SAR</p>
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
                {t("billingVatLabel" as never)} {p.outputVat.toFixed(2)} SAR ·{" "}
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
      </CardContent>
    </Card>
  );
}

function FinanceSettlementsList({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const [cases, setCases] = useState<SettlementCase[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void getPortalDataSource()
      .getSettlementCases()
      .then(setCases)
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
    <div className="space-y-3">
      {cases.map((c) => {
        const summary = deriveSettlementSummary(c.journal);
        return (
          <Card key={c.id}>
            <CardHeader>
              <CardTitle className="text-base">{c.partnerName}</CardTitle>
              <CardDescription>
                {c.externalTransactionId} · {settlementStateLabel(lang, c.state)} ·{" "}
                {summary.sellPrice.amount.toFixed(2)} SAR
              </CardDescription>
            </CardHeader>
          </Card>
        );
      })}
    </div>
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
        <Route
          path="settlements"
          element={
            <div className="space-y-4">
              <FinanceSettlementsList lang={lang} />
              <ModuleScreenRenderer
                screenId="settlement"
                context={{ lang, financeMode: true }}
              />
            </div>
          }
        />
        <Route
          path="reversals"
          element={<ModuleScreenRenderer screenId="reversals" context={{ lang, financeMode: true }} />}
        />
        <Route
          path="billing"
          element={<ModuleScreenRenderer screenId="billing" context={{ lang, financeMode: true }} />}
        />
        <Route path="vat" element={<FinanceVatSummary lang={lang} />} />
        <Route path="pending-approvals" element={<FinancePendingApprovals lang={lang} />} />
        <Route path="*" element={<Navigate to="overview" replace />} />
      </Routes>
    </div>
  );
}
