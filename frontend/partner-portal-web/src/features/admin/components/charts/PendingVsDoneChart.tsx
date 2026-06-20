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
import { ChartCard, ChartEmpty, CHART_COLORS, chartMargin } from "./ChartCard";

interface PendingVsDoneChartProps {
  lang: Lang;
  data: PendingDoneRow[];
}

export function PendingVsDoneChart({ lang, data }: PendingVsDoneChartProps) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";

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

  const legendLabel = (value: string) => {
    const map: Record<string, string> = {
      pending: t("chartLegendPending" as never),
      done: t("chartLegendDone" as never),
      delivered: t("chartLegendDelivered" as never),
      retrying: t("chartLegendRetrying" as never),
      dlq: t("chartLegendDlq" as never),
    };
    return map[value] ?? value;
  };

  return (
    <ChartCard lang={lang} titleKey="chartPendingTitle" descKey="chartPendingDesc">
      {!hasData ? (
        <ChartEmpty lang={lang} />
      ) : (
        <div dir={isRtl ? "rtl" : "ltr"} className="h-[320px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={rows} margin={chartMargin(isRtl)}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
              <XAxis dataKey="name" tick={{ fontSize: 12 }} />
              <YAxis orientation={isRtl ? "right" : "left"} allowDecimals={false} tick={{ fontSize: 12 }} />
              <Tooltip />
              <Legend formatter={legendLabel} />
              <Bar dataKey="done" stackId="status" fill={CHART_COLORS.done} />
              <Bar dataKey="pending" stackId="status" fill={CHART_COLORS.pending} />
              <Bar dataKey="delivered" stackId="status" fill={CHART_COLORS.done} />
              <Bar dataKey="retrying" stackId="status" fill={CHART_COLORS.retrying} />
              <Bar dataKey="dlq" stackId="status" fill={CHART_COLORS.dlq} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </ChartCard>
  );
}
