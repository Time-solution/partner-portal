import { BetaBadge } from "@/components/brand/BetaBadge";
import type { Lang } from "@/lib/i18n";

interface PageHeaderProps {
  title: string;
  description?: string;
  lang: Lang;
  showBeta?: boolean;
}

export function PageHeader({ title, description, lang, showBeta }: PageHeaderProps) {
  return (
    <div className="space-y-1">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="text-2xl font-semibold tracking-tight">{title}</h2>
        {showBeta ? <BetaBadge lang={lang} /> : null}
      </div>
      {description ? <p className="text-base text-muted-foreground">{description}</p> : null}
    </div>
  );
}
