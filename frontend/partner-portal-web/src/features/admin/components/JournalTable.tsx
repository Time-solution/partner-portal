import { MoneyAmount } from "@/components/MoneyAmount";
import type { SettlementJournalLine } from "@/lib/data/types";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { settlementAccountLabel } from "@/lib/i18n/domainLabels";

interface JournalTableProps {
  lang: Lang;
  lines: SettlementJournalLine[];
  caption?: string;
}

const sumDir = (lines: SettlementJournalLine[], dir: "Debit" | "Credit") =>
  lines.filter((l) => l.direction === dir).reduce((s, l) => s + l.amount.amount, 0);

/**
 * Double-entry journal — separate Debit / Credit columns.
 *
 * When lines carry an `entry` tag (e.g. the Principal "Sell" / "Buy" resale entries),
 * each entry is rendered as its own balanced block with a subtotal (sell side = 13,
 * buy side = 10) instead of one inflated combined total. Untagged journals (e.g. the
 * collection/remittance blocks) render unchanged with a single totals row.
 */
export function JournalTable({ lang, lines, caption }: JournalTableProps) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";

  const debits = sumDir(lines, "Debit");
  const credits = sumDir(lines, "Credit");
  const balanced = Math.abs(debits - credits) < 0.01;

  // Preserve first-seen order of entry groups.
  const entryOrder: string[] = [];
  for (const line of lines) {
    const key = line.entry ?? "";
    if (!entryOrder.includes(key)) entryOrder.push(key);
  }
  const grouped = entryOrder.length > 1 || (entryOrder.length === 1 && entryOrder[0] !== "");

  const Row = ({ line, i }: { line: SettlementJournalLine; i: number }) => {
    const isDebit = line.direction === "Debit";
    return (
      <tr key={`${line.account}-${i}`} className="border-b border-border/60 last:border-0">
        <td className="px-3 py-2.5 text-start font-medium">{settlementAccountLabel(lang, line.account)}</td>
        <td className="px-3 py-2.5 text-end tabular-nums">
          {isDebit ? <MoneyAmount amount={line.amount.amount} /> : "—"}
        </td>
        <td className="px-3 py-2.5 text-end tabular-nums">
          {isDebit ? "—" : <MoneyAmount amount={line.amount.amount} />}
        </td>
      </tr>
    );
  };

  return (
    <div className="space-y-2">
      {caption ? <p className="text-sm text-muted-foreground">{caption}</p> : null}
      <div className="overflow-x-auto rounded-md border border-border" dir={isRtl ? "rtl" : "ltr"}>
        <table className="w-full min-w-[520px] text-start text-base">
          <thead>
            <tr className="border-b border-border bg-muted/50 text-sm text-muted-foreground">
              <th className="px-3 py-2.5 text-start font-medium">{t("journalColAccount" as never)}</th>
              <th className="px-3 py-2.5 text-end font-medium">{t("journalColDebit" as never)}</th>
              <th className="px-3 py-2.5 text-end font-medium">{t("journalColCredit" as never)}</th>
            </tr>
          </thead>

          {grouped ? (
            <tbody>
              {entryOrder.map((entryKey) => {
                const entryLines = lines.filter((l) => (l.entry ?? "") === entryKey);
                const entryDebit = sumDir(entryLines, "Debit");
                const entryCredit = sumDir(entryLines, "Credit");
                const entryBalanced = Math.abs(entryDebit - entryCredit) < 0.01;
                const labelKey = `journalEntry_${entryKey}`;
                return (
                  <tr key={`group-${entryKey}`}>
                    <td colSpan={3} className="p-0">
                      <table className="w-full">
                        <tbody>
                          <tr className="border-b border-border bg-muted/30 text-sm">
                            <td className="px-3 py-1.5 text-start font-semibold" colSpan={3}>
                              {t(labelKey as never)}
                            </td>
                          </tr>
                          {entryLines.map((line, i) => (
                            <Row key={`${entryKey}-${line.account}-${i}`} line={line} i={i} />
                          ))}
                          <tr className="border-b border-border bg-muted/20 text-sm font-semibold">
                            <td className="px-3 py-2 text-start">
                              {t("journalSubtotal" as never)}
                              {entryBalanced ? (
                                <span className="ms-2 font-medium text-emerald-600 dark:text-emerald-400">✓</span>
                              ) : (
                                <span className="ms-2 font-medium text-amber-600 dark:text-amber-400">⚠</span>
                              )}
                            </td>
                            <td className="px-3 py-2 text-end tabular-nums">
                              <MoneyAmount amount={entryDebit} />
                            </td>
                            <td className="px-3 py-2 text-end tabular-nums">
                              <MoneyAmount amount={entryCredit} />
                            </td>
                          </tr>
                        </tbody>
                      </table>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          ) : (
            <tbody>
              {lines.map((line, i) => (
                <Row key={`${line.account}-${i}`} line={line} i={i} />
              ))}
            </tbody>
          )}

          <tfoot>
            {!grouped ? (
              <tr className="border-t border-border bg-muted/30 font-semibold">
                <td className="px-3 py-2.5 text-start">{t("journalTotals" as never)}</td>
                <td className="px-3 py-2.5 text-end tabular-nums">
                  <MoneyAmount amount={debits} />
                </td>
                <td className="px-3 py-2.5 text-end tabular-nums">
                  <MoneyAmount amount={credits} />
                </td>
              </tr>
            ) : null}
            <tr className="bg-muted/30">
              <td className="px-3 py-2 text-end text-sm" colSpan={3}>
                <span
                  className={
                    balanced
                      ? "font-medium text-emerald-600 dark:text-emerald-400"
                      : "font-medium text-amber-600 dark:text-amber-400"
                  }
                >
                  {balanced ? `✓ ${t("journalBalanced" as never)}` : `⚠ ${t("journalUnbalanced" as never)}`}
                </span>
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
}
