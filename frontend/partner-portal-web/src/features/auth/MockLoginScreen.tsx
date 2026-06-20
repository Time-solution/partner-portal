import { useCallback, useEffect, useState } from "react";
import { KeyRound, Loader2, LogIn, Mail } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { LanguageToggle } from "@/components/LanguageToggle";
import { BrandLogo } from "@/components/brand/BrandLogo";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { MockDataBanner } from "@/features/admin/components/MockDataBanner";
import { getPortalDataSource } from "@/lib/data";
import type { LoginAccount } from "@/lib/data/types";
import { useMockPortalAuthState } from "@/features/auth/usePortalSession";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

const DEMO_PASSWORD = "demo";

const levelBadgeClass: Record<string, string> = {
  Platform: "bg-primary/15 text-primary",
  Partner: "bg-violet-500/15 text-violet-700 dark:text-violet-300",
  Merchant: "bg-teal-500/15 text-teal-700 dark:text-teal-300",
};

interface MockLoginScreenProps {
  lang: Lang;
  toggleLang: () => void;
  onSuccess: (landing: string) => void;
}

export function MockLoginScreen({ lang, toggleLang, onSuccess }: MockLoginScreenProps) {
  const t = useTranslator(lang);
  const { loginWithEmail } = useMockPortalAuthState();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState(DEMO_PASSWORD);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [accounts, setAccounts] = useState<LoginAccount[]>([]);

  useEffect(() => {
    void getPortalDataSource()
      .listLoginAccounts()
      .then(setAccounts)
      .catch(() => setAccounts([]));
  }, []);

  const submit = useCallback(async () => {
    setError(null);
    setBusy(true);
    try {
      const result = await loginWithEmail(email, password);
      if (result.ok && result.landing) {
        onSuccess(result.landing);
        return;
      }
      setError(
        result.error === "required"
          ? t("loginErrorRequired" as never)
          : t("loginErrorInvalid" as never),
      );
    } finally {
      setBusy(false);
    }
  }, [email, password, loginWithEmail, onSuccess, t]);

  const levelLabel = (level: string) =>
    level === "Platform"
      ? t("orgLevelPlatform" as never)
      : level === "Partner"
        ? t("orgLevelPartner" as never)
        : t("orgLevelMerchant" as never);

  return (
    <div className="relative flex min-h-screen flex-col items-center justify-center bg-background px-4 py-10">
      <div className="absolute top-4 end-4 z-10">
        <LanguageToggle lang={lang} toggleLang={toggleLang} />
      </div>

      <div className="grid w-full max-w-4xl gap-6 md:grid-cols-2">
        <div className="space-y-5">
          <div className="flex flex-col items-start gap-3">
            <BrandLogo variant="full" lang={lang} className="max-w-[200px]" />
            <BetaBadge lang={lang} />
            <h1 className="text-2xl font-semibold tracking-tight">{t("signInTitle")}</h1>
            <p className="text-base text-muted-foreground">{t("signInSubtitle")}</p>
          </div>

          <MockDataBanner lang={lang} />

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-lg">
                <LogIn className="h-5 w-5 text-primary" aria-hidden="true" />
                {t("signIn")}
              </CardTitle>
              <CardDescription>{t("secureNotice")}</CardDescription>
            </CardHeader>
            <CardContent>
              <form
                className="space-y-4"
                onSubmit={(e) => {
                  e.preventDefault();
                  void submit();
                }}
              >
                <div className="space-y-1">
                  <Label htmlFor="login-email">{t("email")}</Label>
                  <div className="relative">
                    <Mail className="pointer-events-none absolute top-2.5 start-3 h-4 w-4 text-muted-foreground" />
                    <Input
                      id="login-email"
                      type="email"
                      autoComplete="username"
                      className="ps-9"
                      placeholder={t("emailPlaceholder")}
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                    />
                  </div>
                </div>
                <div className="space-y-1">
                  <Label htmlFor="login-password">{t("password")}</Label>
                  <div className="relative">
                    <KeyRound className="pointer-events-none absolute top-2.5 start-3 h-4 w-4 text-muted-foreground" />
                    <Input
                      id="login-password"
                      type="password"
                      autoComplete="current-password"
                      className="ps-9"
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                    />
                  </div>
                </div>

                {error ? <p className="text-sm text-destructive">{error}</p> : null}

                <Button type="submit" className="w-full" disabled={busy}>
                  {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : null}
                  {t("signIn")}
                </Button>
              </form>
            </CardContent>
          </Card>
        </div>

        <Card className="self-start">
          <CardHeader>
            <CardTitle className="text-lg">{t("demoAccountsTitle" as never)}</CardTitle>
            <CardDescription>{t("demoAccountsHint" as never)}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-2">
            {accounts.map((acc) => (
              <button
                key={acc.email}
                type="button"
                onClick={() => {
                  setEmail(acc.email);
                  setPassword(DEMO_PASSWORD);
                  setError(null);
                }}
                className="flex items-center justify-between gap-3 rounded-md border border-border px-3 py-2 text-start transition-colors hover:bg-muted"
              >
                <span className="min-w-0">
                  <span className="block truncate font-medium">{acc.name}</span>
                  <span className="block truncate text-xs text-muted-foreground">{acc.email}</span>
                  <span className="block truncate text-xs text-muted-foreground">{acc.orgName}</span>
                </span>
                <span
                  className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${levelBadgeClass[acc.level] ?? ""}`}
                >
                  {levelLabel(acc.level)}
                </span>
              </button>
            ))}
            <p className="pt-1 text-xs text-muted-foreground">{t("demoPasswordHint" as never)}</p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
