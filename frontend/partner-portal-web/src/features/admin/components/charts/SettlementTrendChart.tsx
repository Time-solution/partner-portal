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
import { ChartCard, ChartEmpty, CHART_COLORS, chartMargin, formatSarTooltip } from "./ChartCard";

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

  const rows = useMemo(() => [...data].sort((a, b) => a.periodKey.localeCompare(b.periodKey)), [data]);

  const legendLabel = (value: string) => {
    const map: Record<string, string> = {
      settlementSar: t("chartLegendSettlement" as never),
      marginSar: t("chartLegendMargin" as never),
      vatSar: t("chartLegendVat" as never),
    };
    return map[value] ?? value;
  };

  return (
    <ChartCard lang={lang} titleKey={titleKey} descKey={descKey} showBeta>
      {rows.length === 0 ? (
        <ChartEmpty lang={lang} />
      ) : (
        <div dir={isRtl ? "rtl" : "ltr"} className="h-[320px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={rows} margin={chartMargin(isRtl)}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border/60" />
              <XAxis dataKey="periodKey" tick={{ fontSize: 12 }} />
              <YAxis orientation={isRtl ? "right" : "left"} tick={{ fontSize: 12 }} />
              <Tooltip formatter={(value) => formatSarTooltip(value)} />
              <Legend formatter={legendLabel} />
              <Area type="monotone" dataKey="settlementSar" fill={CHART_COLORS.primary} fillOpacity={0.15} stroke={CHART_COLORS.primary} strokeWidth={2} />
              <Line type="monotone" dataKey="marginSar" stroke={CHART_COLORS.margin} strokeWidth={2} dot={false} />
              <Line type="monotone" dataKey="vatSar" stroke={CHART_COLORS.vat} strokeWidth={2} dot={false} />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      )}
    </ChartCard>
  );
}
