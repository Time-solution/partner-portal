import { useEffect, useState } from "react";
import { Loader2, TrendingUp, Users, Wallet, ShoppingCart } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { DashboardKpis } from "@/lib/data/types";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

export function DashboardPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const [kpis, setKpis] = useState<DashboardKpis | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void getPortalDataSource()
      .getDashboardKpis()
      .then(setKpis)
      .finally(() => setLoading(false));
  }, []);

  if (loading || !kpis) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  const cards = [
    { label: t("kpiPartners" as never), value: kpis.totalPartners, icon: Users },
    { label: t("kpiActivations" as never), value: kpis.activeActivations, icon: TrendingUp },
    { label: t("kpiSettlement" as never), value: `${kpis.settlementTotalSar.toFixed(2)} SAR`, icon: Wallet },
    { label: t("kpiReflected" as never), value: kpis.reflectedOrdersCount, icon: ShoppingCart },
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
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("kpiSubscriptionMtd" as never)}</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-2xl font-bold tabular-nums">{kpis.subscriptionFeesMtd.toFixed(2)} SAR</p>
          <p className="mt-1 text-xs text-muted-foreground">{t("mockSampleNotice" as never)}</p>
        </CardContent>
      </Card>
    </div>
  );
}
