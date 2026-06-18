import { Languages, Loader2, Moon, Sun } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoginScreen } from "@/features/auth/LoginScreen";
import { AdminShell } from "@/features/admin/AdminShell";
import { AuthProvider, useAuth } from "@/features/auth/AuthContext";
import { useTheme } from "@/hooks/useTheme";
import { useLanguage } from "@/hooks/useLanguage";
import { useTranslator } from "@/lib/i18n";

function AppContent() {
  const { theme, toggleTheme } = useTheme();
  const { lang, toggleLang } = useLanguage();
  const { isAuthenticated, isLoading } = useAuth();
  const t = useTranslator(lang);

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background text-muted-foreground">
        <span className="flex items-center gap-2 text-sm">
          <Loader2 className="h-5 w-5 animate-spin" aria-hidden="true" />
          {t("loadingSession")}
        </span>
      </div>
    );
  }

  if (isAuthenticated) {
    return (
      <AdminShell
        lang={lang}
        toggleLang={toggleLang}
        theme={theme}
        toggleTheme={toggleTheme}
      />
    );
  }

  return (
    <div className="relative">
      <div className="absolute top-4 end-4 z-10 flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={toggleLang}
          aria-label={t("toggleToEnglish")}
        >
          <Languages className="h-4 w-4" aria-hidden="true" />
          {t("toggleToEnglish")}
        </Button>
        <Button
          variant="outline"
          size="icon"
          onClick={toggleTheme}
          aria-label={t("toggleTheme")}
        >
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

export default function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  );
}
