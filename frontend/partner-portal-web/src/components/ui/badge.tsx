import * as React from "react";
import { cn } from "@/lib/utils";

export type BadgeVariant = "default" | "success" | "muted" | "outline" | "warning";

const badgeVariants: Record<BadgeVariant, string> = {
  default: "border-transparent bg-primary text-primary-foreground",
  success:
    "border-emerald-500/40 bg-emerald-500/10 text-emerald-800 dark:border-emerald-400/40 dark:text-emerald-200",
  muted: "border-transparent bg-muted text-muted-foreground",
  outline: "border-border text-foreground",
  warning: "border-amber-500/40 bg-amber-500/10 text-amber-900 dark:text-amber-200",
};

export function Badge({
  className,
  variant = "default",
  ...props
}: React.HTMLAttributes<HTMLSpanElement> & { variant?: BadgeVariant }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs font-medium",
        badgeVariants[variant],
        className,
      )}
      {...props}
    />
  );
}
