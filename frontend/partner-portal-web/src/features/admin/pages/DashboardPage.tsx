import { Loader2, RotateCcw, ShoppingCart, TrendingUp, Users } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { usePortalAnalytics } from "@/hooks/usePortalAnalytics";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { canAccessPartnerModules } from "@/lib/rbac/partnerModules";
import type { PortalRole } from "@/lib/rbac/portalRoles";
import { PendingVsDoneChart } from "../components/charts/PendingVsDoneChart";
import { SettlementTrendChart } from "../components/charts/SettlementTrendChart";
import { TransactionsByModuleChart } from "../components/charts/TransactionsByModuleChart";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

export function DashboardPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { role } = usePortalSession();
  const { analytics, loading } = usePortalAnalytics();
  const showCharts = canAccessPartnerModules(role as PortalRole);

  if (loading || !analytics) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  const { kpis } = analytics;

  const cards = [
    { label: t("kpiPartners" as never), value: kpis.activePartners, icon: Users },
    { label: t("kpiActivations" as never), value: kpis.activeActivations, icon: TrendingUp },
    { label: t("kpiReflected" as never), value: kpis.reflectedOrders, icon: ShoppingCart },
    { label: t("kpiReversals" as never), value: kpis.reversals, icon: RotateCcw },
  ];

  return (
    <div className="space-y-6">
      <PageHeader title={t("navDashboard" as never)} description={t("dashboardDesc" as never)} lang={lang} />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {cards.map(({ label, value, icon: Icon }) => (
          <Card key={label}>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle>
              <Icon className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
            </CardHeader>
            <CardContent>
              <p className="text-2xl font-bold tabular-nums">{value}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      {showCharts ? (
        <>
          <TransactionsByModuleChart lang={lang} data={analytics.transactionsByModule} />
          <div className="grid gap-6 xl:grid-cols-2">
            <PendingVsDoneChart lang={lang} data={analytics.pendingVsDone} />
            <SettlementTrendChart lang={lang} data={analytics.settlementTrend} />
          </div>
        </>
      ) : null}
    </div>
  );
}
