import type { ReactNode } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { useTranslator, type Lang } from "@/lib/i18n";
import type { UsageInvoiceBreakdown } from "@/lib/usage/usageInvoiceBreakdown";

const num = (n: number) => new Intl.NumberFormat("en-US").format(n);

/**
 * U4 — the invoice usage BREAKDOWN line (display only). Renders base (prorated if partial) + overage detail
 * (usage, included, excess, rate) = total with the ex-VAT / VAT split, Riyal symbol via {@link MoneyAmount},
 * BETA, AR/EN + RTL. Numbers come from U3 (no recompute). Scope is already applied upstream.
 */
export function UsageInvoiceBreakdownCard({
  lang,
  breakdown,
}: {
  lang: Lang;
  breakdown: UsageInvoiceBreakdown;
}) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const b = breakdown;
  const hasTiers = b.tiers.length > 0;
  const hasOverage = b.overageExcess > 0;
  const tierRange = (from: number, to?: number | null) =>
    to == null ? `${num(from)}+` : `${num(from)}–${num(to)}`;

  const Row = ({ label, children }: { label: string; children: ReactNode }) => (
    <div className="flex items-center justify-between gap-2 border-b border-border/60 py-1.5">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium tabular-nums">{children}</span>
    </div>
  );

  return (
    <Card dir={isRtl ? "rtl" : "ltr"}>
      <CardHeader className="pb-2">
        <CardTitle className="flex items-center gap-2 text-base">
          {t("usageInvTitle" as never)}
          <BetaBadge lang={lang} />
        </CardTitle>
        <CardDescription>
          {b.packageName} · {b.period}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        {/* The headline equation: base + (excess × rate) = total */}
        <p className="flex flex-wrap items-baseline gap-1.5 rounded-md bg-muted/40 px-3 py-2 tabular-nums">
          <span className="text-muted-foreground">{b.prorated ? t("usageInvBaseProrated" as never) : t("usageInvBase" as never)}:</span>
          <MoneyAmount amount={b.baseInclusive} />
          {hasTiers ? (
            b.tiers.map((tier, i) => (
              <span key={i} className="inline-flex items-baseline gap-1">
                <span>+</span>
                <span className="text-muted-foreground">[{tierRange(tier.fromQuantity, tier.toQuantity)}]</span>
                <span>{num(tier.units)}</span>
                <span className="text-muted-foreground">×</span>
                <MoneyAmount amount={tier.rate} />
                <span>=</span>
                <MoneyAmount amount={tier.amountInclusive} />
              </span>
            ))
          ) : hasOverage ? (
            <>
              <span>+</span>
              <span>{num(b.overageExcess)} {b.unitLabel}</span>
              <span className="text-muted-foreground">×</span>
              <MoneyAmount amount={b.overageRate} />
              <span>=</span>
              <MoneyAmount amount={b.overageInclusive} />
            </>
          ) : null}
          <span className="font-semibold">→ {t("usageInvTotal" as never)}:</span>
          <MoneyAmount amount={b.totalInclusive} />
        </p>

        <div>
          <Row label={t("usageInvUsage" as never)}>
            {num(b.usage)} {b.unitLabel}
          </Row>
          <Row label={t("usageInvIncluded" as never)}>
            {num(b.includedQuantity)} {b.unitLabel}
          </Row>
          {hasTiers ? (
            <>
              <Row label={t("usageInvExcess" as never)}>
                {num(b.overageExcess)} {b.unitLabel}
              </Row>
              {b.tiers.map((tier, i) => (
                <Row key={i} label={`${t("usageInvTier" as never)} ${tierRange(tier.fromQuantity, tier.toQuantity)}`}>
                  {num(tier.units)} {b.unitLabel} × <MoneyAmount amount={tier.rate} /> = <MoneyAmount amount={tier.amountInclusive} />
                </Row>
              ))}
              <Row label={t("usageInvOverage" as never)}>
                <MoneyAmount amount={b.overageInclusive} />
              </Row>
            </>
          ) : hasOverage ? (
            <>
              <Row label={t("usageInvExcess" as never)}>
                {num(b.overageExcess)} {b.unitLabel}
              </Row>
              <Row label={t("usageInvRate" as never)}>
                <MoneyAmount amount={b.overageRate} />
              </Row>
              <Row label={t("usageInvOverage" as never)}>
                <MoneyAmount amount={b.overageInclusive} />
              </Row>
            </>
          ) : null}
          <Row label={t("usageInvExVat" as never)}>
            <MoneyAmount amount={b.exVat} />
          </Row>
          <Row label={t("usageInvVat" as never)}>
            <MoneyAmount amount={b.vat} />
          </Row>
          <div className="flex items-center justify-between gap-2 pt-2">
            <span className="font-semibold">{t("usageInvTotal" as never)}</span>
            <span className="text-lg font-bold tabular-nums">
              <MoneyAmount amount={b.totalInclusive} />
            </span>
          </div>
          {/* Admin/accountant only — structurally absent for merchant/partner. */}
          {b.marginInclusive != null ? (
            <Row label={t("usagePkgMargin" as never)}>
              <MoneyAmount amount={b.marginInclusive} />
            </Row>
          ) : null}
        </div>

        {b.prorated ? (
          <p className="text-xs text-muted-foreground">
            {t("usageInvProratedNote" as never)
              .replace("{days}", String(b.activeDays))
              .replace("{dim}", String(b.daysInMonth))}
          </p>
        ) : null}
      </CardContent>
    </Card>
  );
}
