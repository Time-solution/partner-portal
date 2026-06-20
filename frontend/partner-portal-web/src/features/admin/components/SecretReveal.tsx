import { useState } from "react";
import { Check, Copy } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

interface SecretRevealProps {
  lang: Lang;
  label: string;
  secret: string;
  warning: string;
  onDismiss: () => void;
}

export function SecretReveal({ lang, label, secret, warning, onDismiss }: SecretRevealProps) {
  const t = useTranslator(lang);
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    await navigator.clipboard.writeText(secret);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="rounded-lg border border-warning/40 bg-warning/10 p-4 space-y-3">
      <p className="text-sm font-medium text-warning">{warning}</p>
      <div>
        <p className="text-xs text-muted-foreground mb-1">{label}</p>
        <code className="block break-all rounded bg-muted px-3 py-2 font-mono text-sm">{secret}</code>
      </div>
      <div className="flex flex-wrap gap-2">
        <Button size="sm" variant="outline" onClick={() => void copy()}>
          {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
          {copied ? t("copied" as never) : t("copy" as never)}
        </Button>
        <Button size="sm" onClick={onDismiss}>
          {t("dismiss" as never)}
        </Button>
      </div>
    </div>
  );
}
