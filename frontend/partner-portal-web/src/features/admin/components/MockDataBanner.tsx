import { AlertTriangle } from "lucide-react";
import { isMockDataSource } from "@/lib/data/config";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

export function MockDataBanner({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  if (!isMockDataSource()) return null;

  return (
    <div
      role="status"
      className="flex items-center gap-2.5 rounded-md border border-warning/40 bg-warning/10 px-4 py-2.5 text-sm text-warning-color"
    >
      <AlertTriangle className="h-4 w-4 shrink-0" aria-hidden="true" />
      <span>{t("mockDataBanner" as never)}</span>
    </div>
  );
}
