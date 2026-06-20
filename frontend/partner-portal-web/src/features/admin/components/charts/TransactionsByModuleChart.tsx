import { useMemo } from "react";
import {
  Bar,
  CartesianGrid,
  ComposedChart,
  Legend,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { ModuleTransactionPoint } from "@/lib/analytics/chartAnalytics";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { ChartCard, ChartEmpty, CHART_COLORS, chartMargin, formatSarTooltip } from "./ChartCard";

interface TransactionsByModuleChartProps {
  lang: Lang;
  data: ModuleTransactionPoint[];
}

export function TransactionsByModuleChart({ lang, data }: TransactionsByModuleChartProps) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";

  const rows = useMemo(
    () =>
      data.map((row) => ({
        name: t(row.labelKey as never),
        volume: row.volume,
        amountSar: row.amountSar,
      })),
    [data, t],
  );

  const hasData = rows.some((r) => r.volume > 0 || r.amountSar > 0);

  return (
    <ChartCard
      lang={lang}
      titleKey="chartTransactionsTitle"
      descKey="chartTransactionsDesc"
      showBeta
    >
      {!hasData ? (
        <ChartEmpty lang={lang} />
      ) : (
        <div dir={isRtl ? "rtl" : "ltr"} className="h-[320px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={rows} margin={chartMargin(isRtl)}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
              <XAxis dataKey="name" tick={{ fontSize: 12 }} interval={0} angle={isRtl ? 0 : -20} textAnchor={isRtl ? "middle" : "end"} height={60} />
              <YAxis
                yAxisId="volume"
                orientation={isRtl ? "right" : "left"}
                tick={{ fontSize: 12 }}
                allowDecimals={false}
                label={{ value: t("chartLegendVolume" as never), angle: -90, position: isRtl ? "insideRight" : "insideLeft" }}
              />
              <YAxis
                yAxisId="amount"
                orientation={isRtl ? "left" : "right"}
                tick={{ fontSize: 12 }}
                label={{ value: t("chartLegendAmountSar" as never), angle: 90, position: isRtl ? "insideLeft" : "insideRight" }}
              />
              <Tooltip
                formatter={(value, key) => {
                  const n = Number(value ?? 0);
                  return key === "amountSar"
                    ? [formatSarTooltip(n), t("chartLegendAmountSar" as never)]
                    : [n, t("chartLegendVolume" as never)];
                }}
              />
              <Legend formatter={(value) => (value === "amountSar" ? t("chartLegendAmountSar" as never) : t("chartLegendVolume" as never))} />
              <Bar yAxisId="volume" dataKey="volume" fill={CHART_COLORS.volume} radius={[4, 4, 0, 0]} name="volume" />
              <Line yAxisId="amount" type="monotone" dataKey="amountSar" stroke={CHART_COLORS.amount} strokeWidth={2} dot={{ r: 4 }} name="amountSar" />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      )}
    </ChartCard>
  );
}
