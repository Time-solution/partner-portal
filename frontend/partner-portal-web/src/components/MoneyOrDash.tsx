import { MoneyAmount } from "@/components/MoneyAmount";

/** Table cell: Riyal + amount, or em dash when empty/zero. */
export function MoneyOrDash({ amount }: { amount?: number | null }) {
  if (!amount) return <>—</>;
  return <MoneyAmount amount={amount} />;
}
