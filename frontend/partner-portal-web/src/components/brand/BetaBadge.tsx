import { useTranslator, type Lang } from "@/lib/i18n";

interface BetaBadgeProps {
  lang: Lang;
  className?: string;
}

/**
 * Bilingual trial marker shown across the whole cycle (login, app header, etc.).
 * Renders "تجريبي" in Arabic and "Beta" in English; the full notice is the tooltip.
 */
export function BetaBadge({ lang, className }: BetaBadgeProps) {
  const t = useTranslator(lang);
  return (
    <span
      className={[
        "inline-flex items-center rounded-full border border-accent/40 bg-accent/10 px-2 py-0.5",
        "text-[11px] font-semibold uppercase tracking-wide text-accent",
        className,
      ]
        .filter(Boolean)
        .join(" ")}
      title={t("betaNotice" as never)}
    >
      {t("beta" as never)}
    </span>
  );
}
