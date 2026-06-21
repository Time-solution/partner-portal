import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { MoneyAmount } from "./MoneyAmount";
import { MONEY_SYMBOL_GAP_EM } from "@/lib/format/moneyMarkup";

describe("MoneyAmount — Riyal symbol left of the number (SAMA), dir-locked", () => {
  it("renders symbol-then-number order (Riyal SVG before the digits)", () => {
    const html = renderToStaticMarkup(<MoneyAmount amount={45} />);
    expect(html.indexOf("<svg")).toBeLessThan(html.indexOf("45.00"));
  });

  it("locks the money group to LTR so RTL/Arabic cannot flip the symbol right", () => {
    const html = renderToStaticMarkup(<MoneyAmount amount={45} />);
    expect(html).toContain('dir="ltr"');
    expect(html).toContain("flex-row");
  });

  it("uses the ONE shared gap between symbol and number (0.15–0.25em)", () => {
    const html = renderToStaticMarkup(<MoneyAmount amount={45} />);
    expect(html).toContain(`gap:${MONEY_SYMBOL_GAP_EM}em`);
    expect(MONEY_SYMBOL_GAP_EM).toBeGreaterThanOrEqual(0.15);
    expect(MONEY_SYMBOL_GAP_EM).toBeLessThanOrEqual(0.25);
    // no leftover Tailwind gap utility that would compete with the shared em gap
    expect(html).not.toContain("gap-0.5");
  });
});
