import { useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { clampPresentation, MAX_PARTNER_BRIEF } from "@/lib/catalog/presentationFields";
import { useTranslator, type Lang } from "@/lib/i18n";

/**
 * Phase 6b — PARTNER PROFILE brief editor. The company self-introduction that LEADS the merchant
 * view. Editable by EVERY partner type (it is self-description, not pricing). Plain text, trimmed,
 * ≤600, optional (saving empty clears it). Pure/props-driven so it renders in SSR tests.
 */
export function PartnerBriefEditor({
  lang,
  brief,
  canEdit,
  busy = false,
  onSave,
}: {
  lang: Lang;
  brief?: string;
  canEdit: boolean;
  busy?: boolean;
  onSave: (brief: string | undefined) => void;
}) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const [value, setValue] = useState(brief ?? "");
  const len = value.trim().length;

  return (
    <Card data-testid="partner-brief-editor" dir={isRtl ? "rtl" : "ltr"}>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">{t("partnerBriefTitle" as never)}</CardTitle>
        <CardDescription>{t("partnerBriefDesc" as never)}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-2">
        {canEdit ? (
          <>
            <Label htmlFor="partner-brief" className="sr-only">
              {t("partnerBriefTitle" as never)}
            </Label>
            <textarea
              id="partner-brief"
              data-testid="partner-brief-input"
              dir={isRtl ? "rtl" : "ltr"}
              rows={3}
              maxLength={MAX_PARTNER_BRIEF}
              value={value}
              onChange={(e) => setValue(e.target.value)}
              placeholder={t("partnerBriefPlaceholder" as never)}
              className="flex w-full rounded-md border border-input bg-surface px-3 py-2 text-sm text-surface-foreground shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background"
            />
            <div className="flex items-center justify-between gap-2">
              <span className="text-xs text-muted-foreground tabular-nums">
                {len}/{MAX_PARTNER_BRIEF}
              </span>
              <Button
                size="sm"
                data-testid="partner-brief-save"
                disabled={busy}
                onClick={() => onSave(clampPresentation(value, MAX_PARTNER_BRIEF) || undefined)}
              >
                {t("catalogSave" as never)}
              </Button>
            </div>
          </>
        ) : brief ? (
          <p className="whitespace-pre-line text-sm">{brief}</p>
        ) : (
          <p className="text-sm text-muted-foreground">—</p>
        )}
      </CardContent>
    </Card>
  );
}
