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

/** Shared chart colours — works in light/dark. */
export const CHART_COLORS = {
  primary: "hsl(var(--primary))",
  volume: "#6366f1",
  amount: "#0ea5e9",
  pending: "#f59e0b",
  done: "#10b981",
  retrying: "#f97316",
  dlq: "#ef4444",
  margin: "#8b5cf6",
  vat: "#14b8a6",
  revenue: "#3b82f6",
  reversal: "#ec4899",
};

export function chartMargin(isRtl: boolean) {
  return isRtl
    ? { top: 8, right: 8, left: 16, bottom: 8 }
    : { top: 8, right: 16, left: 8, bottom: 8 };
}

export function formatSarTooltip(value: unknown): string {
  return `${Number(value ?? 0).toFixed(2)} SAR`;
}
