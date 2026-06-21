/** Numeric portion only — currency mark is always {@link MoneyAmount} / {@link Riyal}. */
export function formatMoneyNumber(amount: number): string {
  return amount.toFixed(2);
}
