import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { BrandLogo } from "./BrandLogo";

const SRC = join(import.meta.dirname, "..", "..");
const PROJECT = join(SRC, "..");

describe("BrandLogo", () => {
  it("renders the Zahy logo as a contained, non-stretched image", () => {
    const html = renderToStaticMarkup(<BrandLogo />);
    expect(html).toContain("<img");
    expect(html).toContain('alt="Zahy"');
    expect(html).toContain("/brand/zahy-logo.png");
    // No stretch: object-contain + width auto.
    expect(html).toContain("object-contain");
    expect(html).toContain("w-auto");
  });

  it("uses the dark-background artwork when onDark is set", () => {
    const html = renderToStaticMarkup(<BrandLogo onDark />);
    expect(html).toContain("/brand/zahy-logo-dark.png");
    expect(html).not.toContain("/brand/zahy-logo.png");
  });

  it("renders the light artwork by default (light theme / no dark class)", () => {
    const html = renderToStaticMarkup(<BrandLogo onDark={false} />);
    expect(html).toContain("/brand/zahy-logo.png");
    expect(html).not.toContain("zahy-logo-dark.png");
  });

  it("icon variant renders a square mark", () => {
    const html = renderToStaticMarkup(<BrandLogo variant="icon" />);
    expect(html).toContain("/brand/zahy-icon.png");
    expect(html).toContain("h-8 w-8");
  });
});

describe("single source of the logo", () => {
  function walk(dir: string, out: string[] = []) {
    for (const entry of readdirSync(dir)) {
      const full = join(dir, entry);
      if (statSync(full).isDirectory()) walk(full, out);
      else if (/\.(ts|tsx)$/.test(full) && !full.endsWith(".test.tsx") && !full.endsWith(".test.ts")) {
        out.push(full);
      }
    }
    return out;
  }

  it("only BrandLogo.tsx references the raw brand asset paths", () => {
    const offenders = walk(SRC)
      .filter((file) => !file.replace(/\\/g, "/").endsWith("components/brand/BrandLogo.tsx"))
      .filter((file) => /\/brand\/zahy-(logo|icon)/.test(readFileSync(file, "utf8")));

    expect(offenders.map((f) => f.replace(SRC, ""))).toEqual([]);
  });

  it("logo consumers import the shared <BrandLogo /> component", () => {
    const adminShell = readFileSync(join(SRC, "features", "admin", "AdminShell.tsx"), "utf8");
    expect(adminShell).toContain('from "@/components/brand/BrandLogo"');
    expect(adminShell).toContain("<BrandLogo");
  });
});

describe("browser tab (favicon + title)", () => {
  it("index.html sets a Zahy favicon and the Arabic admin title", () => {
    const indexHtml = readFileSync(join(PROJECT, "index.html"), "utf8");
    expect(indexHtml).toMatch(/<link rel="icon"[^>]*href="\/brand\/zahy-icon\.svg"/);
    expect(indexHtml).toContain("<title>Zahy — لوحة الإدارة</title>");
  });

  it("title follows the active language (bilingual-aware, single place)", () => {
    const useLanguage = readFileSync(join(SRC, "hooks", "useLanguage.ts"), "utf8");
    expect(useLanguage).toContain("document.title");
    expect(useLanguage).toContain("Zahy — لوحة الإدارة");
    expect(useLanguage).toContain("Zahy — Admin Console");
  });
});
