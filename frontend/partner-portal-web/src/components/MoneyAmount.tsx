import { formatMoneyNumber } from "@/lib/format/moneyDisplay";
import { MONEY_SYMBOL_GAP_EM } from "@/lib/format/moneyMarkup";
import { Riyal } from "./Riyal";

/**
 * SAMA order: Riyal symbol to the LEFT of the amount. `dir="ltr"` + a fixed flex row keep the
 * symbol on the left in BOTH LTR and RTL (RTL must not flip it). Single display formatter for
 * mock UI money — gap comes from the shared {@link MONEY_SYMBOL_GAP_EM} so all surfaces match.
 */
export function MoneyAmount({ amount, className }: { amount: number; className?: string }) {
  return (
    <span
      dir="ltr"
      style={{ gap: `${MONEY_SYMBOL_GAP_EM}em` }}
      className={["inline-flex flex-row items-baseline tabular-nums", className].filter(Boolean).join(" ")}
    >
      <Riyal />
      <span>{formatMoneyNumber(amount)}</span>
    </span>
  );
}

/** i18n templates with `{amount}` — renders {@link MoneyAmount} at the placeholder. */
export function MoneyInTemplate({ template, amount }: { template: string; amount: number }) {
  const [before, ...rest] = template.split("{amount}");
  const after = rest.join("{amount}");
  return (
    <>
      {before}
      <MoneyAmount amount={amount} />
      {after}
    </>
  );
}
