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
import { ChartCard, ChartEmpty, CHART_COLORS, chartMargin, formatSarTooltip } from "./ChartCard";

export function BillingRevenueChart({ lang, data }: { lang: Lang; data: BillingRevenuePoint[] }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const rows = useMemo(() => [...data], [data]);

  return (
    <ChartCard lang={lang} titleKey="chartBillingRevenueTitle" descKey="chartBillingRevenueDesc" showBeta>
      {rows.length === 0 ? (
        <ChartEmpty lang={lang} />
      ) : (
        <div dir={isRtl ? "rtl" : "ltr"} className="h-[280px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={rows} margin={chartMargin(isRtl)}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
              <XAxis dataKey="periodKey" tick={{ fontSize: 12 }} />
              <YAxis orientation={isRtl ? "right" : "left"} tick={{ fontSize: 12 }} />
              <Tooltip formatter={(value) => formatSarTooltip(value)} />
              <Legend
                formatter={(value) =>
                  value === "revenueSar"
                    ? t("chartLegendRevenue" as never)
                    : t("chartLegendVat" as never)
                }
              />
              <Bar dataKey="revenueSar" fill={CHART_COLORS.revenue} radius={[4, 4, 0, 0]} />
              <Bar dataKey="vatSar" fill={CHART_COLORS.vat} radius={[4, 4, 0, 0]} />
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
            <BarChart data={rows} layout="vertical" margin={chartMargin(isRtl)}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
              <XAxis type="number" tick={{ fontSize: 12 }} />
              <YAxis type="category" dataKey="name" width={120} tick={{ fontSize: 11 }} orientation={isRtl ? "right" : "left"} />
              <Tooltip formatter={(value) => formatSarTooltip(value)} />
              <Bar dataKey="amountSar" fill={CHART_COLORS.reversal} radius={[0, 4, 4, 0]} name={t("chartLegendReversalAmount" as never)} />
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
  const rows = [{ name: t("chartPendingApprovalsLabel" as never), count }];

  return (
    <ChartCard lang={lang} titleKey="chartPendingApprovalsTitle" descKey="chartPendingApprovalsDesc">
      <div dir={isRtl ? "rtl" : "ltr"} className="h-[200px] w-full">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} margin={chartMargin(isRtl)}>
            <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
            <XAxis dataKey="name" tick={{ fontSize: 12 }} />
            <YAxis orientation={isRtl ? "right" : "left"} allowDecimals={false} />
            <Tooltip />
            <Bar dataKey="count" fill={CHART_COLORS.pending} radius={[4, 4, 0, 0]} name={t("chartLegendPending" as never)} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </ChartCard>
  );
}
