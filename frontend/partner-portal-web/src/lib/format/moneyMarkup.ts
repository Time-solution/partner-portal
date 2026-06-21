/**
 * SINGLE SOURCE for money LAYOUT (position + spacing of the Saudi Riyal mark).
 *
 * SAMA convention: the Riyal symbol sits to the LEFT of the numeral. The amount group always
 * reads left-to-right (symbol, then digits) — even inside an RTL/Arabic document — so we lock
 * the group with dir="ltr" + a fixed flex row; RTL never flips the symbol to the right.
 *
 * Every money surface consumes from here: the on-screen <MoneyAmount> (gap constant) and the
 * generated documents (proforma PDF / HTML money cells use {@link moneyMarkup}). Layout only —
 * this file never changes an amount, the glyph, or any math.
 */
import { RIYAL_SVG_PATHS, RIYAL_VIEW_BOX } from "@/components/riyalSymbol";

/** The ONE gap between the Riyal symbol and the number, used everywhere (em-based, consistent). */
export const MONEY_SYMBOL_GAP_EM = 0.2;

/** Inline Riyal SVG (vector, currentColor) — a sharp mark, never a font glyph/tofu. */
export function riyalSvgMarkup(size = "0.85em"): string {
  const paths = RIYAL_SVG_PATHS.map((d) => `<path d="${d}"></path>`).join("");
  return `<svg viewBox="${RIYAL_VIEW_BOX}" role="img" aria-label="ريال" fill="currentColor" style="height:${size};width:auto;flex:none;">${paths}</svg>`;
}

function formatAmount(amount: string | number): string {
  return typeof amount === "number"
    ? amount.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    : amount;
}

/**
 * A money group as an HTML string: the Riyal symbol to the LEFT of the number with the single
 * shared gap. dir="ltr" + inline-flex row keep the symbol on the left in BOTH LTR and RTL.
 */
export function moneyMarkup(amount: string | number, size = "0.85em"): string {
  return (
    `<span dir="ltr" style="display:inline-flex;flex-direction:row;align-items:baseline;` +
    `gap:${MONEY_SYMBOL_GAP_EM}em;white-space:nowrap;font-variant-numeric:tabular-nums;">` +
    `${riyalSvgMarkup(size)}<span>${formatAmount(amount)}</span></span>`
  );
}
