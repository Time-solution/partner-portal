import { useState } from "react";
import type { Lang } from "@/lib/i18n";

interface BrandLogoProps {
  /** "full" = horizontal wordmark lockup; "icon" = compact mark. */
  variant?: "full" | "icon";
  className?: string;
  lang?: Lang;
  /** Prefer dark-background artwork (e.g. header on navy). */
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

export function BrandLogo({
  variant = "full",
  className,
  lang = "ar",
  onDark = false,
}: BrandLogoProps) {
  const [failed, setFailed] = useState(false);
  const pack = ASSETS[variant];
  const src = onDark ? pack.dark : pack.light;

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
