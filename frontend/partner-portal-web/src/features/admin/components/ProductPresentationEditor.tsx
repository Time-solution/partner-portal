import { useState } from "react";
import { ArrowRight, ArrowLeft } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import {
  clampPresentation,
  MAX_MERCHANT_BENEFIT,
  MAX_OFFERING_SUMMARY,
  MAX_PACKAGE_EXPLANATION,
} from "@/lib/catalog/presentationFields";
import type { PartnerCatalogItem } from "@/lib/data/types";
import type { UsagePackage } from "@/lib/usage/usagePackage";
import { useTranslator, type Lang } from "@/lib/i18n";
import { PackageDetailsCard } from "./PackageDetailsCard";

const TEXTAREA_CLASS =
  "flex w-full rounded-md border border-input bg-surface px-3 py-2 text-sm text-surface-foreground shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background";

/** Per-package note editor (partner-authored PackageExplanation, ≤500, optional). */
function PackageNoteEditor({
  lang,
  pkg,
  busy,
  onSave,
}: {
  lang: Lang;
  pkg: UsagePackage;
  busy: boolean;
  onSave: (note: string | undefined) => void;
}) {
  const t = useTranslator(lang);
  const [note, setNote] = useState(pkg.packageExplanation ?? "");
  const len = note.trim().length;
  return (
    <div className="mt-2 space-y-1">
      <Label htmlFor={`pkg-note-${pkg.id}`} className="text-xs text-muted-foreground">
        {t("packageExplanationLabel" as never)}
      </Label>
      <textarea
        id={`pkg-note-${pkg.id}`}
        data-testid="package-note-input"
        rows={2}
        maxLength={MAX_PACKAGE_EXPLANATION}
        value={note}
        onChange={(e) => setNote(e.target.value)}
        placeholder={t("packageExplanationPlaceholder" as never)}
        className={TEXTAREA_CLASS}
      />
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs text-muted-foreground tabular-nums">
          {len}/{MAX_PACKAGE_EXPLANATION}
        </span>
        <Button
          size="sm"
          variant="outline"
          disabled={busy}
          onClick={() => onSave(clampPresentation(note, MAX_PACKAGE_EXPLANATION) || undefined)}
        >
          {t("catalogSave" as never)}
        </Button>
      </div>
    </div>
  );
}

/**
 * Phase 6b — PRODUCT EDITOR (drill-in). Edits the offering presentation (MerchantBenefit +
 * OfferingSummary/description) and shows the partner's PACKAGES with read-only structured details
 * plus an optional partner-authored note. Authoring is gated by the authoring-by-type rule: a service
 * partner self-authors; delivery/3PL fields render read-only (admin-managed). Pure/props-driven.
 */
export function ProductPresentationEditor({
  lang,
  item,
  packages,
  canEdit,
  busy = false,
  onBack,
  onSaveOffering,
  onSavePackageNote,
}: {
  lang: Lang;
  item: PartnerCatalogItem;
  packages: UsagePackage[];
  canEdit: boolean;
  busy?: boolean;
  onBack: () => void;
  onSaveOffering: (input: { description?: string; merchantBenefit?: string }) => void;
  onSavePackageNote: (packageId: string, note: string | undefined) => void;
}) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const BackIcon = isRtl ? ArrowRight : ArrowLeft;
  const [benefit, setBenefit] = useState(item.merchantBenefit ?? "");
  const [summary, setSummary] = useState(item.description ?? "");

  return (
    <div className="space-y-4" data-testid="product-presentation-editor" dir={isRtl ? "rtl" : "ltr"}>
      <div className="flex items-center justify-between gap-2">
        <Button size="sm" variant="ghost" onClick={onBack}>
          <BackIcon className="h-4 w-4" />
          {t("productBackToList" as never)}
        </Button>
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">
            {t("productPresentationTitle" as never)} · {item.name}
            <span className="ms-2 font-mono text-xs text-muted-foreground">{item.code}</span>
          </CardTitle>
          <CardDescription>{t("productPresentationDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {!canEdit ? (
            <p className="rounded-md border border-border bg-muted/40 px-3 py-2 text-sm text-muted-foreground">
              {t("presentationReadOnlyNotice" as never)}
            </p>
          ) : null}

          {canEdit ? (
            <>
              <div className="space-y-1">
                <Label htmlFor="benefit">{t("merchantBenefitLabel" as never)}</Label>
                <textarea
                  id="benefit"
                  data-testid="benefit-input"
                  rows={2}
                  maxLength={MAX_MERCHANT_BENEFIT}
                  value={benefit}
                  onChange={(e) => setBenefit(e.target.value)}
                  placeholder={t("merchantBenefitPlaceholder" as never)}
                  className={TEXTAREA_CLASS}
                />
                <span className="text-xs text-muted-foreground tabular-nums">
                  {benefit.trim().length}/{MAX_MERCHANT_BENEFIT}
                </span>
              </div>
              <div className="space-y-1">
                <Label htmlFor="summary">{t("offeringSummaryLabel" as never)}</Label>
                <textarea
                  id="summary"
                  data-testid="summary-input"
                  rows={2}
                  maxLength={MAX_OFFERING_SUMMARY}
                  value={summary}
                  onChange={(e) => setSummary(e.target.value)}
                  placeholder={t("offeringSummaryPlaceholder" as never)}
                  className={TEXTAREA_CLASS}
                />
                <span className="text-xs text-muted-foreground tabular-nums">
                  {summary.trim().length}/{MAX_OFFERING_SUMMARY}
                </span>
              </div>
              <div className="flex items-center justify-between gap-2">
                <span className="text-xs text-muted-foreground">{t("presentationOptionalHint" as never)}</span>
                <Button
                  size="sm"
                  data-testid="offering-save"
                  disabled={busy}
                  onClick={() =>
                    onSaveOffering({
                      merchantBenefit: clampPresentation(benefit, MAX_MERCHANT_BENEFIT) || undefined,
                      description: clampPresentation(summary, MAX_OFFERING_SUMMARY) || undefined,
                    })
                  }
                >
                  {t("catalogSave" as never)}
                </Button>
              </div>
            </>
          ) : (
            <dl className="space-y-2 text-sm">
              <div>
                <dt className="text-muted-foreground">{t("merchantBenefitLabel" as never)}</dt>
                <dd className="whitespace-pre-line">{item.merchantBenefit ?? "—"}</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">{t("offeringSummaryLabel" as never)}</dt>
                <dd className="whitespace-pre-line">{item.description ?? "—"}</dd>
              </div>
            </dl>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">{t("productPackagesTitle" as never)}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {packages.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("productNoPackages" as never)}</p>
          ) : (
            packages.map((pkg) => (
              <div key={pkg.id} className="space-y-1">
                <PackageDetailsCard lang={lang} pkg={pkg} />
                {canEdit ? (
                  <PackageNoteEditor
                    lang={lang}
                    pkg={pkg}
                    busy={busy}
                    onSave={(note) => onSavePackageNote(pkg.id, note)}
                  />
                ) : null}
              </div>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
