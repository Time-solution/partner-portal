import { MoneyAmount } from "@/components/MoneyAmount";
import {
  assertMerchantPackageScope,
  type MerchantPackageDisplay,
} from "@/lib/usage/usagePackageDisplay";
import { useTranslator, type Lang } from "@/lib/i18n";

/**
 * Phase 6b — read-only MERCHANT view of a package's structured details + the optional partner note.
 * Prop hygiene: this merchant-facing component receives the ALREADY-SCOPED display (built by the
 * single merchantPackageDisplay mapper upstream) — never the raw UsagePackage with its buy fields.
 * SELL/fee only — partner buy and margin are structurally absent (asserted defensively).
 */
export function PackageDetailsCard({ lang, display }: { lang: Lang; display: MerchantPackageDisplay }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const d = display;
  assertMerchantPackageScope(d);

  const tierBound = (to?: number | null) =>
    to == null ? "+" : `–${to.toLocaleString(isRtl ? "ar-SA" : "en-GB")}`;

  return (
    <div
      data-testid="package-details"
      dir={isRtl ? "rtl" : "ltr"}
      className="rounded-md border border-border bg-muted/30 px-3 py-2 text-sm"
    >
      <p className="font-medium">{d.name}</p>
      <dl className="mt-1 grid gap-x-4 gap-y-1 sm:grid-cols-2">
        <div className="flex items-center justify-between gap-2">
          <dt className="text-muted-foreground">{t("pkgDetailModeLabel" as never)}</dt>
          <dd>
            {t((d.mode === "Subscription" ? "usagePkgModeSubscription" : "usagePkgModeResale") as never)}
          </dd>
        </div>
        <div className="flex items-center justify-between gap-2">
          <dt className="text-muted-foreground">{t("pkgDetailIncludedLabel" as never)}</dt>
          <dd className="tabular-nums">
            {d.includedQuantity.toLocaleString(isRtl ? "ar-SA" : "en-GB")} {d.unitLabel}
          </dd>
        </div>
        {d.overageRate != null ? (
          <div className="flex items-center justify-between gap-2">
            <dt className="text-muted-foreground">{t("pkgDetailOverageLabel" as never)}</dt>
            <dd className="flex items-center gap-1">
              <MoneyAmount amount={d.overageRate} />
              <span className="text-xs text-muted-foreground">/ {d.unitLabel}</span>
            </dd>
          </div>
        ) : null}
      </dl>

      {d.tiers.length > 0 ? (
        <div className="mt-2">
          <p className="text-xs font-medium text-muted-foreground">{t("pkgDetailTiersLabel" as never)}</p>
          <ul className="mt-1 space-y-0.5">
            {d.tiers.map((tier, i) => (
              <li key={i} className="flex items-center justify-between gap-2 tabular-nums">
                <span>
                  {tier.fromQuantity.toLocaleString(isRtl ? "ar-SA" : "en-GB")}
                  {tierBound(tier.toQuantity)} {d.unitLabel}
                </span>
                {tier.rate != null ? (
                  <span className="flex items-center gap-1">
                    <MoneyAmount amount={tier.rate} />
                    <span className="text-xs text-muted-foreground">/ {d.unitLabel}</span>
                  </span>
                ) : null}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {d.explanation ? (
        <div className="mt-2 border-t border-border/60 pt-2">
          <p className="text-xs font-medium text-muted-foreground">{t("pkgDetailNoteLabel" as never)}</p>
          <p className="mt-0.5 whitespace-pre-line text-sm">{d.explanation}</p>
        </div>
      ) : null}
    </div>
  );
}
