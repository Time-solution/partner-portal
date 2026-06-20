import type { SettlementJournalLine } from "@/lib/data/types";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import {
  journalDirectionLabel,
  settlementAccountLabel,
} from "@/lib/i18n/domainLabels";

interface JournalTableProps {
  lang: Lang;
  lines: SettlementJournalLine[];
  caption?: string;
}

export function JournalTable({ lang, lines, caption }: JournalTableProps) {
  const t = useTranslator(lang);
  const debits = lines
    .filter((l) => l.direction === "Debit")
    .reduce((s, l) => s + l.amount.amount, 0);
  const credits = lines
    .filter((l) => l.direction === "Credit")
    .reduce((s, l) => s + l.amount.amount, 0);
  const balanced = Math.abs(debits - credits) < 0.01;

  return (
    <div className="space-y-2">
      {caption ? <p className="text-sm text-muted-foreground">{caption}</p> : null}
      <div className="overflow-x-auto rounded-md border border-border">
        <table className="w-full min-w-[520px] text-base">
          <thead>
            <tr className="border-b border-border bg-muted/50 text-start text-muted-foreground">
              <th className="px-3 py-2.5 font-medium">{t("journalColAccount" as never)}</th>
              <th className="px-3 py-2.5 font-medium">{t("journalColDirection" as never)}</th>
              <th className="px-3 py-2.5 text-end font-medium">{t("journalColAmount" as never)}</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line, i) => (
              <tr key={`${line.account}-${i}`} className="border-b border-border/60 last:border-0">
                <td className="px-3 py-2.5 font-mono text-sm">
                  {settlementAccountLabel(lang, line.account)}
                </td>
                <td className="px-3 py-2.5">{journalDirectionLabel(lang, line.direction)}</td>
                <td className="px-3 py-2.5 text-end tabular-nums">{line.amount.amount.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="bg-muted/30 font-medium">
              <td className="px-3 py-2.5" colSpan={2}>
                {t("journalTotals" as never)}
              </td>
              <td className="px-3 py-2.5 text-end text-sm">
                {t("journalDrCr" as never)
                  .replace("{dr}", debits.toFixed(2))
                  .replace("{cr}", credits.toFixed(2))}
                {balanced ? ` ✓ (${t("journalBalanced" as never)})` : ` ⚠ (${t("journalUnbalanced" as never)})`}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
}
