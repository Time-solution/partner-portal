import { Moon, Sun } from "lucide-react";
import { BrowserRouter, useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { LanguageToggle } from "@/components/LanguageToggle";
import { LoginScreen } from "@/features/auth/LoginScreen";
import { AdminShell } from "@/features/admin/AdminShell";
import { AuthProvider, useAuth } from "@/features/auth/AuthContext";
import { MockPortalAuthProvider } from "@/features/auth/MockPortalAuth";
import { MockLoginScreen } from "@/features/auth/MockLoginScreen";
import { useMockPortalAuthState } from "@/features/auth/usePortalSession";
import { isMockDataSource } from "@/lib/data/config";
import { useTheme } from "@/hooks/useTheme";
import { useLanguage } from "@/hooks/useLanguage";
import { useTranslator } from "@/lib/i18n";
import { Loader2 } from "lucide-react";

function LiveAppContent() {
  const { theme, toggleTheme } = useTheme();
  const { lang, toggleLang } = useLanguage();
  const { isAuthenticated, isLoading } = useAuth();
  const t = useTranslator(lang);

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background text-muted-foreground">
        <span className="flex items-center gap-2 text-base">
          <Loader2 className="h-5 w-5 animate-spin" aria-hidden="true" />
          {t("loadingSession")}
        </span>
      </div>
    );
  }

  if (isAuthenticated) {
    return (
      <BrowserRouter>
        <AdminShell lang={lang} toggleLang={toggleLang} theme={theme} toggleTheme={toggleTheme} />
      </BrowserRouter>
    );
  }

  return (
    <div className="relative">
      <div className="absolute top-4 end-4 z-10 flex items-center gap-2">
        <LanguageToggle lang={lang} toggleLang={toggleLang} />
        <Button variant="outline" size="icon" onClick={toggleTheme} aria-label={t("toggleTheme")}>
          {theme === "dark" ? (
            <Sun className="h-4 w-4" aria-hidden="true" />
          ) : (
            <Moon className="h-4 w-4" aria-hidden="true" />
          )}
        </Button>
      </div>

      <LoginScreen lang={lang} />
    </div>
  );
}

function MockLoginRoute({
  lang,
  toggleLang,
}: {
  lang: ReturnType<typeof useLanguage>["lang"];
  toggleLang: () => void;
}) {
  const navigate = useNavigate();

  return (
    <MockLoginScreen
      lang={lang}
      toggleLang={toggleLang}
      onSuccess={(landing: string) => navigate(landing, { replace: true })}
    />
  );
}

function MockAppContent() {
  const { theme, toggleTheme } = useTheme();
  const { lang, toggleLang } = useLanguage();
  const { isAuthenticated } = useMockPortalAuthState();

  return (
    <BrowserRouter>
      {isAuthenticated ? (
        <AdminShell lang={lang} toggleLang={toggleLang} theme={theme} toggleTheme={toggleTheme} />
      ) : (
        <MockLoginRoute lang={lang} toggleLang={toggleLang} />
      )}
    </BrowserRouter>
  );
}

export default function App() {
  if (isMockDataSource()) {
    return (
      <MockPortalAuthProvider>
        <MockAppContent />
      </MockPortalAuthProvider>
    );
  }

  return (
    <AuthProvider>
      <LiveAppContent />
    </AuthProvider>
  );
}
