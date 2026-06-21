import { useEffect, useState } from "react";
import type { Lang } from "@/lib/i18n";

interface BrandLogoProps {
  /** "full" = horizontal wordmark lockup; "icon" = compact mark. */
  variant?: "full" | "icon";
  className?: string;
  lang?: Lang;
  /**
   * Force the dark-background artwork. Leave undefined to auto-follow the active
   * theme (the `.dark` class on <html>) — the single place logo variant is decided.
   */
  onDark?: boolean;
}

const ASSETS = {
  full: {
    light: "/brand/zahy-logo.png",
    dark: "/brand/zahy-logo-dark.png",
    svg: "/brand/zahy-logo.svg",
  },
  icon: {
    light: "/brand/zahy-icon.png",
    dark: "/brand/zahy-icon.png",
    svg: "/brand/zahy-icon.svg",
  },
} as const;

/**
 * The light full-lockup logo path — the SINGLE place the raw brand asset URL lives. Non-React
 * consumers that cannot render <BrandLogo /> (e.g. the proforma PDF's HTML string) import this
 * instead of hard-coding the path, so brand asset references stay in one file.
 */
export const ZAHY_LOGO_SRC = ASSETS.full.light;

/** Tracks the active theme by watching the `.dark` class on <html> (set by useTheme). */
function useIsDarkTheme() {
  const [isDark, setIsDark] = useState(
    () => typeof document !== "undefined" && document.documentElement.classList.contains("dark"),
  );

  useEffect(() => {
    if (typeof document === "undefined") return;
    const el = document.documentElement;
    const sync = () => setIsDark(el.classList.contains("dark"));
    sync();
    const observer = new MutationObserver(sync);
    observer.observe(el, { attributes: true, attributeFilter: ["class"] });
    return () => observer.disconnect();
  }, []);

  return isDark;
}

export function BrandLogo({
  variant = "full",
  className,
  lang = "ar",
  onDark,
}: BrandLogoProps) {
  const [failed, setFailed] = useState(false);
  const isDarkTheme = useIsDarkTheme();
  const pack = ASSETS[variant];
  // Explicit prop wins; otherwise follow the active theme so the dark-bg art is
  // used on the dark UI and the light-bg art on the light UI.
  const src = (onDark ?? isDarkTheme) ? pack.dark : pack.light;

  if (failed) {
    const wordmark =
      variant === "icon" ? (lang === "ar" ? "ز" : "Z") : lang === "ar" ? "زاهي" : "Zahy";
    return (
      <span
        className={[
          "font-bold tracking-tight text-primary",
          variant === "icon" ? "text-lg" : "text-xl",
          className,
        ]
          .filter(Boolean)
          .join(" ")}
        aria-label="Zahy"
      >
        {wordmark}
      </span>
    );
  }

  return (
    <img
      src={src}
      alt="Zahy"
      className={[
        "object-contain object-start",
        variant === "full" ? "h-8 max-w-[160px] w-auto" : "h-8 w-8",
        className,
      ]
        .filter(Boolean)
        .join(" ")}
      onError={() => setFailed(true)}
      draggable={false}
    />
  );
}
