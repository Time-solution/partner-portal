import { Languages } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

interface LanguageToggleProps {
  lang: Lang;
  toggleLang: () => void;
  size?: "sm" | "icon";
}

export function LanguageToggle({ lang, toggleLang, size = "sm" }: LanguageToggleProps) {
  const t = useTranslator(lang);

  if (size === "icon") {
    return (
      <Button variant="outline" size="icon" onClick={toggleLang} aria-label={t("toggleLanguage" as never)}>
        <Languages className="h-4 w-4" aria-hidden="true" />
      </Button>
    );
  }

  return (
    <Button variant="outline" size="sm" onClick={toggleLang} aria-label={t("toggleLanguage" as never)}>
      <Languages className="h-4 w-4" aria-hidden="true" />
      {t("toggleToEnglish" as never)}
    </Button>
  );
}
