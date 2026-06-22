import { useTranslator, type Lang } from "@/lib/i18n";
import { FINANCE_SIDES, type FinanceSide } from "@/lib/filters/financeSide";

const LABEL_KEY: Record<FinanceSide, string> = {
  both: "sideFilterBoth",
  sales: "sideFilterSales",
  purchase: "sideFilterPurchase",
};

/**
 * Shared purchase/sales toggle. Drives the single {@link FinanceSide} state (via `useFinanceSide`)
 * that finance views narrow by. Bilingual + RTL-aware. Display/filter only — scope/absence is
 * enforced in the read layer (`financeSide.ts`), not here, so this control stays dumb.
 */
export function PurchaseSalesFilter({
  side,
  onChange,
  lang,
  className,
}: {
  side: FinanceSide;
  onChange: (next: FinanceSide) => void;
  lang: Lang;
  className?: string;
}) {
  const t = useTranslator(lang);
  return (
    <div
      dir={lang === "ar" ? "rtl" : "ltr"}
      data-testid="purchase-sales-filter"
      role="group"
      aria-label={t("sideFilterLabel" as never)}
      className={["inline-flex rounded-md border border-input bg-background p-0.5", className].filter(Boolean).join(" ")}
    >
      {FINANCE_SIDES.map((value) => {
        const active = side === value;
        return (
          <button
            key={value}
            type="button"
            aria-pressed={active}
            onClick={() => onChange(value)}
            className={[
              "rounded px-3 py-1 text-sm font-medium transition-colors",
              active ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:text-foreground",
            ].join(" ")}
          >
            {t(LABEL_KEY[value] as never)}
          </button>
        );
      })}
    </div>
  );
}
