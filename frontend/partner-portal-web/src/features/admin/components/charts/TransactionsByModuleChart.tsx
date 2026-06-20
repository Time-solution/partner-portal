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
import { ChartCard, ChartEmpty, CHART_COLORS } from "./ChartCard";
import { chartMargins, chartTick, volumeAmountTooltipFormatter } from "./chartI18n";

interface TransactionsByModuleChartProps {
  lang: Lang;
  data: ModuleTransactionPoint[];
}

export function TransactionsByModuleChart({ lang, data }: TransactionsByModuleChartProps) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";

  const volumeLabel = t("chartLegendVolume" as never);
  const amountLabel = t("chartLegendAmountSar" as never);

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
        <div dir={isRtl ? "rtl" : "ltr"} className="h-[360px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={rows} margin={chartMargins(isRtl, { categoryAxis: true, yAxisLabel: true })}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
              <XAxis
                dataKey="name"
                tick={chartTick(isRtl)}
                interval={0}
                angle={isRtl ? 0 : -25}
                textAnchor={isRtl ? "middle" : "end"}
                height={isRtl ? 64 : 72}
                tickMargin={8}
              />
              <YAxis
                yAxisId="volume"
                orientation={isRtl ? "right" : "left"}
                tick={chartTick(isRtl)}
                allowDecimals={false}
                width={48}
                label={{
                  value: volumeLabel,
                  angle: -90,
                  position: isRtl ? "insideRight" : "insideLeft",
                  style: { fontSize: 11, textAnchor: "middle" },
                }}
              />
              <YAxis
                yAxisId="amount"
                orientation={isRtl ? "left" : "right"}
                tick={chartTick(isRtl)}
                width={48}
                label={{
                  value: amountLabel,
                  angle: 90,
                  position: isRtl ? "insideLeft" : "insideRight",
                  style: { fontSize: 11, textAnchor: "middle" },
                }}
              />
              <Tooltip formatter={volumeAmountTooltipFormatter(lang, t)} />
              <Legend />
              <Bar
                yAxisId="volume"
                dataKey="volume"
                name={volumeLabel}
                fill={CHART_COLORS.volume}
                radius={[4, 4, 0, 0]}
              />
              <Line
                yAxisId="amount"
                type="monotone"
                dataKey="amountSar"
                name={amountLabel}
                stroke={CHART_COLORS.amount}
                strokeWidth={2}
                dot={{ r: 4 }}
              />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      )}
    </ChartCard>
  );
}
