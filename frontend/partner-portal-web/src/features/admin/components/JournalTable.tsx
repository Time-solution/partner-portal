import type { SettlementJournalLine } from "@/lib/data/types";

interface JournalTableProps {
  lines: SettlementJournalLine[];
  caption?: string;
}

export function JournalTable({ lines, caption }: JournalTableProps) {
  const debits = lines
    .filter((l) => l.direction === "Debit")
    .reduce((s, l) => s + l.amount.amount, 0);
  const credits = lines
    .filter((l) => l.direction === "Credit")
    .reduce((s, l) => s + l.amount.amount, 0);

  return (
    <div className="space-y-2">
      {caption ? <p className="text-sm text-muted-foreground">{caption}</p> : null}
      <div className="overflow-x-auto rounded-md border border-border">
        <table className="w-full min-w-[520px] text-base">
          <thead>
            <tr className="border-b border-border bg-muted/50 text-start text-muted-foreground">
              <th className="px-3 py-2.5 font-medium">Account</th>
              <th className="px-3 py-2.5 font-medium">Direction</th>
              <th className="px-3 py-2.5 text-end font-medium">Amount (SAR)</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line, i) => (
              <tr key={`${line.account}-${i}`} className="border-b border-border/60 last:border-0">
                <td className="px-3 py-2.5 font-mono text-sm">{line.account}</td>
                <td className="px-3 py-2.5">{line.direction}</td>
                <td className="px-3 py-2.5 text-end tabular-nums">{line.amount.amount.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="bg-muted/30 font-medium">
              <td className="px-3 py-2.5" colSpan={2}>
                Totals
              </td>
              <td className="px-3 py-2.5 text-end text-sm">
                DR {debits.toFixed(2)} = CR {credits.toFixed(2)}
                {Math.abs(debits - credits) < 0.01 ? " ✓" : " ⚠"}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
}
