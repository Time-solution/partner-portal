import { describe, expect, it } from "vitest";
import { ReportAccount } from "./settlementReports";
import { SALES_ACCOUNTS, PURCHASE_ACCOUNTS, accountSide } from "@/lib/filters/financeSide";
import { translations } from "@/lib/i18n";

/**
 * F12 pin — the three FE label surfaces (report account names, i18n display labels, sales/purchase
 * export partition) resolve consistently for the FULL canonical account list. The backend twin
 * (SettlementAccountVocabularyTests) pins code ↔ enum ↔ report-name over the same list — this
 * hardcoded mirror is the cross-side drift guard. Display vocabulary only: codes, types and the
 * canonical chart count are untouched.
 */

/** The 11 canonical settlement chart accounts — mirror of SettlementAccountVocabulary.ReportNames. */
const CANONICAL_REPORT_NAMES = [
  "Cash", // 1100
  "MerchantReceivable", // 1200
  "PartnerReceivable", // 1250 (backend-only)
  "VatInput", // 1300
  "PartnerPayable", // 2100
  "VatOutput", // 2200
  "VatControl", // 2300 (backend-only)
  "ReflectionClearing", // 2400 (backend-only)
  "RevenueNetSell", // 4100
  "FeeRevenue", // 4200
  "PartnerCost", // 5100
] as const;

describe("F12 — one chart vocabulary across reports, statements, exports", () => {
  it("the FE report surface carries the 8 chart names shared with the backend, spelled identically", () => {
    const feNames = new Set<string>(Object.values(ReportAccount));
    const backendOnly = new Set(["PartnerReceivable", "VatControl", "ReflectionClearing"]);
    for (const name of CANONICAL_REPORT_NAMES) {
      if (backendOnly.has(name)) continue; // control/clearing codes never render in FE mock journals
      expect(feNames.has(name), `ReportAccount is missing canonical name '${name}'`).toBe(true);
    }
  });

  it("the statements/display surface (i18n) resolves an AR + EN label for the FULL canonical list", () => {
    for (const name of CANONICAL_REPORT_NAMES) {
      const key = `settlementAccount_${name}`;
      expect(translations.ar[key], `missing AR label for ${key}`).toBeTruthy();
      expect(translations.en[key], `missing EN label for ${key}`).toBeTruthy();
      expect(translations.ar[key]).not.toBe(translations.en[key]); // real translations, not copies
    }
  });

  it("every FE report account (incl. COD-only names) also resolves an AR + EN display label", () => {
    for (const name of Object.values(ReportAccount)) {
      expect(translations.ar[`settlementAccount_${name}`], `missing AR label for ${name}`).toBeTruthy();
      expect(translations.en[`settlementAccount_${name}`], `missing EN label for ${name}`).toBeTruthy();
    }
  });

  it("the export/filter surface partitions the full canonical list deterministically", () => {
    const expectedSides: Record<string, ReturnType<typeof accountSide>> = {
      Cash: "neutral",
      MerchantReceivable: "sales",
      PartnerReceivable: "neutral",
      VatInput: "purchase",
      PartnerPayable: "purchase",
      VatOutput: "sales",
      VatControl: "neutral",
      ReflectionClearing: "neutral",
      RevenueNetSell: "sales",
      FeeRevenue: "sales",
      PartnerCost: "purchase",
    };
    for (const name of CANONICAL_REPORT_NAMES) {
      expect(accountSide(name), `side of ${name}`).toBe(expectedSides[name]);
    }
    // The side sets never overlap and only contain known report names.
    for (const account of SALES_ACCOUNTS) expect(PURCHASE_ACCOUNTS.has(account)).toBe(false);
    const feNames = new Set<string>(Object.values(ReportAccount));
    for (const account of [...SALES_ACCOUNTS, ...PURCHASE_ACCOUNTS]) {
      expect(feNames.has(account), `${account} must be a ReportAccount name`).toBe(true);
    }
  });
});
