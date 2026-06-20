import { ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { LanguageToggle } from "@/components/LanguageToggle";
import { BrandLogo } from "@/components/brand/BrandLogo";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { MockDataBanner } from "@/features/admin/components/MockDataBanner";
import { PORTAL_ROLES, roleLabels, type PortalRole } from "@/lib/rbac/portalRoles";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

interface MockRoleSelectScreenProps {
  lang: Lang;
  toggleLang: () => void;
  onLogin: (role: PortalRole) => void;
}

export function MockRoleSelectScreen({ lang, toggleLang, onLogin }: MockRoleSelectScreenProps) {
  const t = useTranslator(lang);

  return (
    <div className="relative flex min-h-screen flex-col items-center justify-center bg-background px-4 py-10">
      <div className="absolute top-4 end-4 z-10">
        <LanguageToggle lang={lang} toggleLang={toggleLang} />
      </div>

      <div className="w-full max-w-lg space-y-6">
        <div className="flex flex-col items-center gap-3 text-center">
          <BrandLogo variant="full" lang={lang} className="max-w-[200px]" />
          <BetaBadge lang={lang} />
          <h1 className="text-2xl font-semibold tracking-tight">{t("mockLoginTitle" as never)}</h1>
          <p className="text-base text-muted-foreground">{t("mockLoginDesc" as never)}</p>
        </div>

        <MockDataBanner lang={lang} />

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-lg">
              <ShieldCheck className="h-5 w-5 text-primary" aria-hidden="true" />
              {t("mockSelectRole" as never)}
            </CardTitle>
            <CardDescription>{t("mockSelectRoleDesc" as never)}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-2">
            {PORTAL_ROLES.map((role) => (
              <Button
                key={role}
                variant="outline"
                className="h-auto justify-start py-3 text-start text-base"
                onClick={() => onLogin(role)}
              >
                <span className="font-medium">{roleLabels[role][lang]}</span>
              </Button>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
