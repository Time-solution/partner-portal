import type { Lang } from "@/lib/i18n";
import { formatMoneyNumber } from "@/lib/format/moneyDisplay";

/** @deprecated Chart tooltips use {@link MoneyChartTooltipContent} — numeric fallback only. */
export function formatMoneyTooltip(value: unknown, _lang: Lang): string {
  return formatMoneyNumber(Number(value ?? 0));
}

/** @deprecated Use formatMoneyTooltip(value, lang) */
export function formatSarTooltip(value: unknown): string {
  return formatMoneyTooltip(value, "en");
}

export interface ChartMarginOptions {
  /** Extra room for multi-line or angled category labels */
  categoryAxis?: boolean;
  /** Extra room for Y-axis series label (vertical charts) */
  yAxisLabel?: boolean;
}

export function chartMargins(isRtl: boolean, options: ChartMarginOptions = {}) {
  const { categoryAxis, yAxisLabel } = options;
  const bottom = categoryAxis ? (isRtl ? 32 : 56) : 16;
  const side = yAxisLabel ? 56 : isRtl ? 32 : 24;
  return {
    top: 16,
    right: isRtl ? 12 : side,
    left: isRtl ? side : 12,
    bottom,
  };
}

export function chartTick(isRtl: boolean) {
  return {
    fontSize: 13,
    fontWeight: 500,
    fill: "hsl(var(--foreground))",
    ...(isRtl ? { direction: "rtl" as const } : {}),
  };
}

/** Larger, clearer legend with bigger swatches. */
export function legendProps(isRtl: boolean) {
  return {
    iconSize: 14,
    iconType: "circle" as const,
    wrapperStyle: {
      fontSize: 13,
      fontWeight: 500,
      paddingTop: 8,
      direction: (isRtl ? "rtl" : "ltr") as "rtl" | "ltr",
    },
  };
}

/** Readable tooltip — solid surface, larger text, RTL-aware. */
export function tooltipProps(isRtl: boolean) {
  return {
    contentStyle: {
      fontSize: 13,
      borderRadius: 8,
      border: "1px solid hsl(var(--border))",
      background: "hsl(var(--popover))",
      color: "hsl(var(--popover-foreground))",
      direction: (isRtl ? "rtl" : "ltr") as "rtl" | "ltr",
    },
    labelStyle: { fontWeight: 600, marginBottom: 4 },
    cursor: { fill: "hsl(var(--muted))", fillOpacity: 0.4 },
  };
}

type Translate = (key: never) => string;

/** Count/count-style tooltip — series names must already be translated via `name` prop. */
export function countTooltipFormatter(value: unknown, name: unknown): [string, string] {
  return [String(Number(value ?? 0)), String(name ?? "")];
}

/** Money tooltip — series label from translated `name` prop or lookup. */
export function moneyTooltipFormatter(lang: Lang) {
  return (value: unknown, name: unknown): [string, string] => [
    formatMoneyTooltip(value, lang),
    String(name ?? ""),
  ];
}

/** Mixed volume (count) + amount series on one chart. */
export function volumeAmountTooltipFormatter(lang: Lang, t: Translate) {
  return (value: unknown, name: unknown): [string, string] => {
    const key = String(name ?? "");
    if (key === t("chartLegendAmountSar" as never) || key === "amountSar") {
      return [formatMoneyTooltip(value, lang), t("chartLegendAmountSar" as never)];
    }
    return [String(Number(value ?? 0)), t("chartLegendVolume" as never)];
  };
}
