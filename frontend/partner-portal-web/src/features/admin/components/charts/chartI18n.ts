import type { Lang } from "@/lib/i18n";

/** Money tooltip — locale-correct currency suffix (no mixed EN "SAR" in Arabic UI). */
export function formatMoneyTooltip(value: unknown, lang: Lang): string {
  const amount = Number(value ?? 0).toFixed(2);
  return lang === "ar" ? `${amount} ر.س` : `${amount} SAR`;
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
    fontSize: 12,
    ...(isRtl ? { direction: "rtl" as const } : {}),
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
