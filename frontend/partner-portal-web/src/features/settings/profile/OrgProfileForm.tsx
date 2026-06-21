import { useCallback, useEffect, useState } from "react";
import { CheckCircle2, Loader2 } from "lucide-react";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getPortalDataSource } from "@/lib/data";
import {
  hasOrgProfileErrors,
  validateOrgProfile,
  type OrgProfile,
  type OrgProfileFieldErrors,
  type OrgProfileScope,
} from "@/lib/profile/orgProfile";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

interface OrgProfileFormProps {
  lang: Lang;
  scope: OrgProfileScope;
  requireNationalNumber: boolean;
  titleKey: string;
  descKey: string;
}

function fieldLabel(lang: Lang, key: string): string {
  const labels: Record<string, { ar: string; en: string }> = {
    name: { ar: "الاسم", en: "Name" },
    vatNumber: { ar: "الرقم الضريبي", en: "VAT number" },
    crNumber: { ar: "السجل التجاري", en: "Commercial registration (CR)" },
    nationalNumber: { ar: "الرقم الوطني", en: "National number" },
    address: { ar: "العنوان", en: "Address" },
    phone: { ar: "الهاتف", en: "Phone" },
    email: { ar: "البريد الإلكتروني", en: "Email" },
  };
  return labels[key]?.[lang] ?? key;
}

function errorMessage(lang: Lang, code: string | undefined): string | undefined {
  if (!code) return undefined;
  if (code === "required") return lang === "ar" ? "حقل مطلوب" : "Required";
  return lang === "ar" ? "صيغة غير صحيحة" : "Invalid format";
}

export function OrgProfileForm({
  lang,
  scope,
  requireNationalNumber,
  titleKey,
  descKey,
}: OrgProfileFormProps) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const [profile, setProfile] = useState<OrgProfile | null>(null);
  const [errors, setErrors] = useState<OrgProfileFieldErrors>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getPortalDataSource().getOrgProfile(scope);
      setProfile(data);
      setErrors({});
    } finally {
      setLoading(false);
    }
  }, [scope.kind, scope.id]);

  useEffect(() => {
    void load();
  }, [load]);

  const update = (patch: Partial<OrgProfile>) => {
    setProfile((prev) =>
      prev
        ? {
            ...prev,
            ...patch,
            contacts: { ...prev.contacts, ...(patch.contacts ?? {}) },
          }
        : prev,
    );
    setSaved(false);
  };

  const save = async () => {
    if (!profile) return;
    const nextErrors = validateOrgProfile(profile, { requireNationalNumber });
    setErrors(nextErrors);
    if (hasOrgProfileErrors(nextErrors)) return;

    setSaving(true);
    try {
      const savedProfile = await getPortalDataSource().saveOrgProfile(scope, profile);
      setProfile(savedProfile);
      setSaved(true);
    } finally {
      setSaving(false);
    }
  };

  if (loading || !profile) {
    return (
      <div className="flex items-center gap-2 text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  return (
    <Card dir={isRtl ? "rtl" : "ltr"}>
      <CardHeader>
        <div className="flex flex-wrap items-center gap-2">
          <CardTitle className="text-lg">{t(titleKey as never)}</CardTitle>
          <BetaBadge lang={lang} />
        </div>
        <CardDescription>{t(descKey as never)}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2 sm:col-span-2">
            <Label htmlFor="org-name">{fieldLabel(lang, "name")}</Label>
            <Input
              id="org-name"
              value={profile.name}
              onChange={(e) => update({ name: e.target.value })}
              aria-invalid={!!errors.name}
            />
            {errors.name ? (
              <p className="text-sm text-destructive">{errorMessage(lang, errors.name)}</p>
            ) : null}
          </div>

          <div className="space-y-2">
            <Label htmlFor="org-vat">{fieldLabel(lang, "vatNumber")}</Label>
            <Input
              id="org-vat"
              inputMode="numeric"
              value={profile.vatNumber}
              onChange={(e) => update({ vatNumber: e.target.value.replace(/\D/g, "").slice(0, 15) })}
              aria-invalid={!!errors.vatNumber}
            />
            {errors.vatNumber ? (
              <p className="text-sm text-destructive">{errorMessage(lang, errors.vatNumber)}</p>
            ) : null}
          </div>

          <div className="space-y-2">
            <Label htmlFor="org-cr">{fieldLabel(lang, "crNumber")}</Label>
            <Input
              id="org-cr"
              inputMode="numeric"
              value={profile.crNumber}
              onChange={(e) => update({ crNumber: e.target.value.replace(/\D/g, "").slice(0, 10) })}
              aria-invalid={!!errors.crNumber}
            />
            {errors.crNumber ? (
              <p className="text-sm text-destructive">{errorMessage(lang, errors.crNumber)}</p>
            ) : null}
          </div>

          {requireNationalNumber ? (
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="org-national">{fieldLabel(lang, "nationalNumber")}</Label>
              <Input
                id="org-national"
                inputMode="numeric"
                value={profile.nationalNumber ?? ""}
                onChange={(e) =>
                  update({ nationalNumber: e.target.value.replace(/\D/g, "").slice(0, 10) })
                }
                aria-invalid={!!errors.nationalNumber}
              />
              {errors.nationalNumber ? (
                <p className="text-sm text-destructive">{errorMessage(lang, errors.nationalNumber)}</p>
              ) : null}
            </div>
          ) : null}

          <div className="space-y-2 sm:col-span-2">
            <Label htmlFor="org-address">{fieldLabel(lang, "address")}</Label>
            <Input
              id="org-address"
              value={profile.address}
              onChange={(e) => update({ address: e.target.value })}
              aria-invalid={!!errors.address}
            />
            {errors.address ? (
              <p className="text-sm text-destructive">{errorMessage(lang, errors.address)}</p>
            ) : null}
          </div>

          <div className="space-y-2">
            <Label htmlFor="org-phone">{fieldLabel(lang, "phone")}</Label>
            <Input
              id="org-phone"
              type="tel"
              dir="ltr"
              className="text-start"
              value={profile.contacts.phone}
              onChange={(e) => update({ contacts: { ...profile.contacts, phone: e.target.value } })}
              aria-invalid={!!errors.phone}
            />
            {errors.phone ? (
              <p className="text-sm text-destructive">{errorMessage(lang, errors.phone)}</p>
            ) : null}
          </div>

          <div className="space-y-2">
            <Label htmlFor="org-email">{fieldLabel(lang, "email")}</Label>
            <Input
              id="org-email"
              type="email"
              dir="ltr"
              className="text-start"
              value={profile.contacts.email}
              onChange={(e) => update({ contacts: { ...profile.contacts, email: e.target.value } })}
              aria-invalid={!!errors.email}
            />
            {errors.email ? (
              <p className="text-sm text-destructive">{errorMessage(lang, errors.email)}</p>
            ) : null}
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <Button disabled={saving} onClick={() => void save()}>
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            {t("orgProfileSave" as never)}
          </Button>
          {saved ? (
            <span className="flex items-center gap-1 text-sm text-emerald-600">
              <CheckCircle2 className="h-4 w-4" />
              {t("orgProfileSaved" as never)}
            </span>
          ) : null}
        </div>
      </CardContent>
    </Card>
  );
}
