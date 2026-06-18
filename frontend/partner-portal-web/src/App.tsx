import { Languages, Moon, Sun } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoginScreen } from "@/features/auth/LoginScreen";
import { useTheme } from "@/hooks/useTheme";
import { useLanguage } from "@/hooks/useLanguage";
import { useTranslator } from "@/lib/i18n";

export default function App() {
  const { theme, toggleTheme } = useTheme();
  const { lang, toggleLang } = useLanguage();
  const t = useTranslator(lang);

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
