import { useMemo } from "react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { BillingRevenuePoint, ReversalImpactPoint } from "@/lib/analytics/chartAnalytics";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { ChartCard, ChartEmpty, CHART_COLORS } from "./ChartCard";
import { chartMargins, chartTick, countTooltipFormatter, moneyTooltipFormatter } from "./chartI18n";

export function BillingRevenueChart({ lang, data }: { lang: Lang; data: BillingRevenuePoint[] }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const rows = useMemo(() => [...data], [data]);
  const revenueLabel = t("chartLegendRevenue" as never);
  const vatLabel = t("chartLegendVat" as never);

  return (
    <ChartCard lang={lang} titleKey="chartBillingRevenueTitle" descKey="chartBillingRevenueDesc" showBeta>
      {rows.length === 0 ? (
        <ChartEmpty lang={lang} />
      ) : (
        <div dir={isRtl ? "rtl" : "ltr"} className="h-[280px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={rows} margin={chartMargins(isRtl, { yAxisLabel: true })}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
              <XAxis dataKey="periodKey" tick={chartTick(isRtl)} tickMargin={8} />
              <YAxis orientation={isRtl ? "right" : "left"} tick={chartTick(isRtl)} width={56} />
              <Tooltip formatter={moneyTooltipFormatter(lang)} />
              <Legend />
              <Bar dataKey="revenueSar" name={revenueLabel} fill={CHART_COLORS.revenue} radius={[4, 4, 0, 0]} />
              <Bar dataKey="vatSar" name={vatLabel} fill={CHART_COLORS.vat} radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </ChartCard>
  );
}

export function ReversalsImpactChart({ lang, data }: { lang: Lang; data: ReversalImpactPoint[] }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const amountLabel = t("chartLegendReversalAmount" as never);
  const rows = useMemo(
    () => data.map((r) => ({ name: r.partnerName, amountSar: r.amountSar })),
    [data],
  );

  return (
    <ChartCard lang={lang} titleKey="chartReversalsTitle" descKey="chartReversalsDesc" showBeta>
      {rows.length === 0 ? (
        <ChartEmpty lang={lang} />
      ) : (
        <div dir={isRtl ? "rtl" : "ltr"} className="h-[280px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={rows} layout="vertical" margin={chartMargins(isRtl, { yAxisLabel: true })}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
              <XAxis type="number" tick={chartTick(isRtl)} />
              <YAxis
                type="category"
                dataKey="name"
                width={isRtl ? 140 : 128}
                tick={chartTick(isRtl)}
                orientation={isRtl ? "right" : "left"}
              />
              <Tooltip formatter={moneyTooltipFormatter(lang)} />
              <Bar
                dataKey="amountSar"
                name={amountLabel}
                fill={CHART_COLORS.reversal}
                radius={[0, 4, 4, 0]}
              />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </ChartCard>
  );
}

export function PendingApprovalsChart({ lang, count }: { lang: Lang; count: number }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const categoryLabel = t("chartPendingApprovalsLabel" as never);
  const pendingLabel = t("chartLegendPending" as never);
  const rows = [{ name: categoryLabel, count }];

  return (
    <ChartCard lang={lang} titleKey="chartPendingApprovalsTitle" descKey="chartPendingApprovalsDesc">
      <div dir={isRtl ? "rtl" : "ltr"} className="h-[220px] w-full">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} margin={chartMargins(isRtl, { categoryAxis: true })}>
            <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
            <XAxis dataKey="name" tick={chartTick(isRtl)} interval={0} height={48} tickMargin={8} />
            <YAxis orientation={isRtl ? "right" : "left"} allowDecimals={false} tick={chartTick(isRtl)} width={40} />
            <Tooltip formatter={countTooltipFormatter} />
            <Legend />
            <Bar dataKey="count" name={pendingLabel} fill={CHART_COLORS.pending} radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </ChartCard>
  );
}
