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
import type { PendingDoneRow } from "@/lib/analytics/chartAnalytics";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { ChartCard, ChartEmpty, CHART_COLORS } from "./ChartCard";
import { chartMargins, chartTick, countTooltipFormatter } from "./chartI18n";

interface PendingVsDoneChartProps {
  lang: Lang;
  data: PendingDoneRow[];
}

export function PendingVsDoneChart({ lang, data }: PendingVsDoneChartProps) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";

  const labels = useMemo(
    () => ({
      done: t("chartLegendDone" as never),
      pending: t("chartLegendPending" as never),
      delivered: t("chartLegendDelivered" as never),
      retrying: t("chartLegendRetrying" as never),
      dlq: t("chartLegendDlq" as never),
    }),
    [t],
  );

  const rows = useMemo(
    () =>
      data.map((row) => {
        const isWebhooks = row.categoryKey === "chartCategory_webhooks";
        return {
          name: t(row.categoryKey as never),
          pending: isWebhooks ? 0 : row.pending,
          done: isWebhooks ? 0 : row.done,
          delivered: isWebhooks ? row.done : 0,
          retrying: isWebhooks ? (row.retrying ?? 0) : 0,
          dlq: isWebhooks ? (row.dlq ?? 0) : 0,
        };
      }),
    [data, t],
  );

  const hasData = rows.some((r) => r.pending + r.done + r.delivered + r.retrying + r.dlq > 0);

  return (
    <ChartCard lang={lang} titleKey="chartPendingTitle" descKey="chartPendingDesc">
      {!hasData ? (
        <ChartEmpty lang={lang} />
      ) : (
        <div dir={isRtl ? "rtl" : "ltr"} className="h-[340px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={rows} margin={chartMargins(isRtl, { categoryAxis: true })}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
              <XAxis
                dataKey="name"
                tick={chartTick(isRtl)}
                interval={0}
                height={isRtl ? 56 : 48}
                tickMargin={8}
              />
              <YAxis
                orientation={isRtl ? "right" : "left"}
                allowDecimals={false}
                tick={chartTick(isRtl)}
                width={isRtl ? 48 : 40}
              />
              <Tooltip formatter={countTooltipFormatter} />
              <Legend />
              <Bar dataKey="done" name={labels.done} stackId="status" fill={CHART_COLORS.done} />
              <Bar dataKey="pending" name={labels.pending} stackId="status" fill={CHART_COLORS.pending} />
              <Bar dataKey="delivered" name={labels.delivered} stackId="status" fill={CHART_COLORS.done} />
              <Bar dataKey="retrying" name={labels.retrying} stackId="status" fill={CHART_COLORS.retrying} />
              <Bar dataKey="dlq" name={labels.dlq} stackId="status" fill={CHART_COLORS.dlq} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </ChartCard>
  );
}
