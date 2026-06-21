import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import type { ReflectedPartnerOrder } from "@/lib/data/types";
import { useTranslator, type Lang } from "@/lib/i18n";
import {
  buildReflectionDetailRows,
  type ReflectionViewer,
} from "@/lib/reflection/reflectionView";

export function ReflectionDetailPanel({
  order,
  lang,
  viewer = "full",
}: {
  order: ReflectedPartnerOrder;
  lang: Lang;
  viewer?: ReflectionViewer;
}) {
  const t = useTranslator(lang);
  const rows = buildReflectionDetailRows(order, viewer, lang, t);
  const isMerchant = viewer === "merchant";

  return (
    <div className="max-w-xl space-y-1">
      <div className="mb-1 flex items-center gap-2">
        <h4 className="text-sm font-semibold">
          {t(isMerchant ? ("merchantReflectionDetailTitle" as never) : ("reflectionDetailTitle" as never))}
        </h4>
        {!isMerchant ? <BetaBadge lang={lang} /> : null}
      </div>
      <p className="mb-2 text-xs text-muted-foreground">
        {t(isMerchant ? ("merchantReflectionDetailDesc" as never) : ("reflectionDetailDesc" as never))}
      </p>
      {rows.map((row) =>
        row.value ? (
          <div
            key={row.labelKey}
            className="flex items-center justify-between gap-4 border-b border-border/50 py-1.5"
          >
            <span className="text-muted-foreground">{row.label}</span>
            <span className="font-medium tabular-nums">
              {row.moneyAmount != null ? <MoneyAmount amount={row.moneyAmount} /> : row.value}
            </span>
          </div>
        ) : (
          <p key={row.labelKey} className="border-b border-border/50 py-1.5 text-sm font-medium">
            {row.label}
          </p>
        ),
      )}
    </div>
  );
}
