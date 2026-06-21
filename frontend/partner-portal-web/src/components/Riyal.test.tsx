import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { Riyal } from "./Riyal";
import { RIYAL_VIEW_BOX } from "./riyalSymbol";

describe("Riyal", () => {
  it("renders inline SVG with currentColor and SAMA viewBox", () => {
    const html = renderToStaticMarkup(<Riyal />);
    expect(html).toContain(`viewBox="${RIYAL_VIEW_BOX}"`);
    expect(html).toContain('fill="currentColor"');
    expect(html).not.toContain("#231f20");
    expect(html).not.toContain("cls-1");
  });

  it("exposes Arabic accessibility label and scales with text", () => {
    const html = renderToStaticMarkup(<Riyal />);
    expect(html).toContain('aria-label="ريال"');
    expect(html).toContain('role="img"');
    expect(html).toContain('height:1em');
    expect(html).toContain("vertical-align:baseline");
  });
});
