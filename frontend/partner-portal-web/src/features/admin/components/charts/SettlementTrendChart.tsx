import { useMemo } from "react";
import {
  Area,
  CartesianGrid,
  ComposedChart,
  Legend,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { SettlementTrendPoint } from "@/lib/analytics/chartAnalytics";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { ChartCard, ChartEmpty, CHART_COLORS } from "./ChartCard";
import { chartMargins, chartTick, moneyTooltipFormatter } from "./chartI18n";

interface SettlementTrendChartProps {
  lang: Lang;
  data: SettlementTrendPoint[];
  titleKey?: string;
  descKey?: string;
}

export function SettlementTrendChart({
  lang,
  data,
  titleKey = "chartSettlementTrendTitle",
  descKey = "chartSettlementTrendDesc",
}: SettlementTrendChartProps) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";

  const settlementLabel = t("chartLegendSettlement" as never);
  const marginLabel = t("chartLegendMargin" as never);
  const vatLabel = t("chartLegendVat" as never);

  const rows = useMemo(() => [...data].sort((a, b) => a.periodKey.localeCompare(b.periodKey)), [data]);

  return (
    <ChartCard lang={lang} titleKey={titleKey} descKey={descKey} showBeta>
      {rows.length === 0 ? (
        <ChartEmpty lang={lang} />
      ) : (
        <div dir={isRtl ? "rtl" : "ltr"} className="h-[320px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={rows} margin={chartMargins(isRtl, { yAxisLabel: true })}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
              <XAxis dataKey="periodKey" tick={chartTick(isRtl)} tickMargin={8} />
              <YAxis orientation={isRtl ? "right" : "left"} tick={chartTick(isRtl)} width={56} />
              <Tooltip formatter={moneyTooltipFormatter(lang)} />
              <Legend />
              <Area
                type="monotone"
                dataKey="settlementSar"
                name={settlementLabel}
                fill={CHART_COLORS.primary}
                fillOpacity={0.15}
                stroke={CHART_COLORS.primary}
                strokeWidth={2}
              />
              <Line
                type="monotone"
                dataKey="marginSar"
                name={marginLabel}
                stroke={CHART_COLORS.margin}
                strokeWidth={2}
                dot={false}
              />
              <Line
                type="monotone"
                dataKey="vatSar"
                name={vatLabel}
                stroke={CHART_COLORS.vat}
                strokeWidth={2}
                dot={false}
              />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      )}
    </ChartCard>
  );
}
