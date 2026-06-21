import type { ReactNode } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { BetaBadge } from "@/components/brand/BetaBadge";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

interface ChartCardProps {
  lang: Lang;
  titleKey: string;
  descKey: string;
  showBeta?: boolean;
  children: ReactNode;
}

export function ChartCard({ lang, titleKey, descKey, showBeta, children }: ChartCardProps) {
  const t = useTranslator(lang);

  return (
    <Card>
      <CardHeader className="space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <CardTitle className="text-lg">{t(titleKey as never)}</CardTitle>
          {showBeta ? <BetaBadge lang={lang} /> : null}
        </div>
        <CardDescription>{t(descKey as never)}</CardDescription>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

export function ChartEmpty({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  return (
    <p className="flex h-[280px] items-center justify-center text-sm text-muted-foreground">
      {t("chartNoData" as never)}
    </p>
  );
}

/** Shared chart colours — high-contrast, accessible, visually distinct in light/dark. */
export const CHART_COLORS = {
  primary: "#2563eb",
  settlement: "#2563eb",
  volume: "#4f46e5",
  amount: "#0284c7",
  pending: "#d97706",
  done: "#059669",
  retrying: "#ea580c",
  dlq: "#dc2626",
  margin: "#7c3aed",
  vat: "#0d9488",
  revenue: "#2563eb",
  reversal: "#db2777",
};

export { chartMargins, formatMoneyTooltip, formatSarTooltip } from "./chartI18n";
