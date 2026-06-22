import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { PurchaseSalesFilter } from "./PurchaseSalesFilter";

describe("PurchaseSalesFilter — shared toggle", () => {
  it("renders Both / Sales / Purchase options with the active one pressed", () => {
    const html = renderToStaticMarkup(<PurchaseSalesFilter side="sales" onChange={() => {}} lang="en" />);
    expect(html).toContain('data-testid="purchase-sales-filter"');
    expect(html).toContain("All"); // both
    expect(html).toContain("Sales");
    expect(html).toContain("Purchase");
    expect(html).toContain('aria-pressed="true"'); // the active "Sales"
  });

  it("is RTL for Arabic, LTR for English", () => {
    const ar = renderToStaticMarkup(<PurchaseSalesFilter side="both" onChange={() => {}} lang="ar" />);
    const en = renderToStaticMarkup(<PurchaseSalesFilter side="both" onChange={() => {}} lang="en" />);
    expect(ar).toContain('dir="rtl"');
    expect(ar).toContain("مبيعات"); // Sales (AR)
    expect(en).toContain('dir="ltr"');
  });
});
