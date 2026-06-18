import { useEffect, useState } from "react";
import type { Lang } from "@/lib/i18n";

const STORAGE_KEY = "zahy-lang";

export function useLanguage() {
  const [lang, setLang] = useState<Lang>(() => {
    if (typeof window === "undefined") return "ar";
    return (window.localStorage.getItem(STORAGE_KEY) as Lang | null) ?? "ar";
  });

  useEffect(() => {
    const root = document.documentElement;
    root.lang = lang;
    root.dir = lang === "ar" ? "rtl" : "ltr";
    window.localStorage.setItem(STORAGE_KEY, lang);
  }, [lang]);

  return {
    lang,
    toggleLang: () => setLang((l) => (l === "ar" ? "en" : "ar")),
  };
}
