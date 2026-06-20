import { Clock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { formatIdleCountdown } from "@/lib/auth/sessionIdle";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

interface SessionIdleWarningProps {
  lang: Lang;
  remainingMs: number;
  onStaySignedIn: () => void;
}

export function SessionIdleWarning({ lang, remainingMs, onStaySignedIn }: SessionIdleWarningProps) {
  const t = useTranslator(lang);

  return (
    <div
      role="alertdialog"
      aria-labelledby="session-idle-title"
      aria-describedby="session-idle-desc"
      className="fixed inset-x-4 bottom-4 z-50 mx-auto max-w-lg rounded-lg border border-warning/40 bg-card p-4 shadow-lg sm:inset-x-auto sm:end-6 sm:bottom-6"
    >
      <div className="flex items-start gap-3">
        <Clock className="mt-0.5 h-5 w-5 shrink-0 text-warning" aria-hidden="true" />
        <div className="min-w-0 flex-1 space-y-2">
          <p id="session-idle-title" className="font-medium text-foreground">
            {t("sessionIdleWarningTitle" as never)}
          </p>
          <p id="session-idle-desc" className="text-sm text-muted-foreground">
            {t("sessionIdleWarningBody" as never).replace(
              "{time}",
              formatIdleCountdown(remainingMs),
            )}
          </p>
          <Button size="sm" onClick={onStaySignedIn}>
            {t("sessionIdleStaySignedIn" as never)}
          </Button>
        </div>
      </div>
    </div>
  );
}
