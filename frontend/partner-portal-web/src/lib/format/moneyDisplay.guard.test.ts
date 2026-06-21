import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const ROOT = join(import.meta.dirname, "..", "..");
const DISPLAY_ROOTS = ["features", "components"];

const FORBIDDEN_PATTERNS = [
  /\}\s*SAR[`'"]/,
  /[`'"]\s*\+\s*[`'"]SAR[`'"]/,
  /\$\{[^}]+\}\s*SAR/,
  /\.toFixed\(2\)\}\s*SAR/,
  /\+\s*[`'"]\s*SAR[`'"]/,
  />\s*SAR\s*</,
  /ر\.س/,
  /﷼/,
  /U\+FDFC/,
];

function walk(dir: string, out: string[] = []) {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) walk(full, out);
    else if (full.endsWith(".tsx")) out.push(full);
  }
  return out;
}

describe("money display guard", () => {
  it("UI tsx files do not render legacy SAR / ر.س / ﷼ currency marks", () => {
    const files = DISPLAY_ROOTS.flatMap((part) => walk(join(ROOT, part)));
    const offenders: string[] = [];

    for (const file of files) {
      const text = readFileSync(file, "utf8");
      for (const pattern of FORBIDDEN_PATTERNS) {
        if (pattern.test(text)) {
          offenders.push(`${file.replace(ROOT + "\\", "").replace(ROOT + "/", "")}: ${pattern}`);
          break;
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it("MoneyAmount is the shared formatter entry point", () => {
    const moneyAmountPath = join(ROOT, "components", "MoneyAmount.tsx");
    const source = readFileSync(moneyAmountPath, "utf8");
    expect(source).toContain('from "./Riyal"');
    expect(source).toContain("formatMoneyNumber");
  });
});
