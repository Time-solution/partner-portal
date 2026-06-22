import { describe, expect, it } from "vitest";
import { ReportAccount } from "@/lib/reports/settlementReports";
import {
  accountSide,
  accountVisible,
  applyFinanceSideToParams,
  canSeeContribution,
  filterAccountsBySide,
  financeSideFromParams,
  financeSideScope,
  marginVisible,
  netVatVisible,
  purchaseVisible,
  salesVisible,
  type FinanceSide,
} from "./financeSide";
import { applyDateRangeToParams, dateRangeFromParams } from "./dateRange";

const A = ReportAccount;
const PLATFORM = financeSideScope("Accountant");
const PARTNER = financeSideScope("PartnerFinance");
const MERCHANT = financeSideScope("MerchantPreview");

// A full account set spanning sales / purchase / neutral.
const allAccounts = [
  { account: A.RevenueNetSell, debit: 0, credit: 100 }, // sales
  { account: A.FeeRevenue, debit: 0, credit: 20 }, // sales
  { account: A.MerchantReceivable, debit: 138, credit: 0 }, // sales
  { account: A.VatOutput, debit: 0, credit: 18 }, // sales
  { account: A.PartnerCost, debit: 60, credit: 0 }, // purchase
  { account: A.PartnerPayable, debit: 0, credit: 69 }, // purchase
  { account: A.VatInput, debit: 9, credit: 0 }, // purchase
  { account: A.Cash, debit: 50, credit: 0 }, // neutral
  { account: A.CollectionsReceivable, debit: 30, credit: 0 }, // neutral
];
const accountsOf = (side: FinanceSide, scope = PLATFORM) =>
  filterAccountsBySide(allAccounts, side, scope).map((a) => a.account);

describe("accountSide — classification (existing accounts only)", () => {
  it("maps sales / purchase / neutral", () => {
    expect(accountSide(A.RevenueNetSell)).toBe("sales");
    expect(accountSide(A.FeeRevenue)).toBe("sales");
    expect(accountSide(A.MerchantReceivable)).toBe("sales");
    expect(accountSide(A.VatOutput)).toBe("sales");
    expect(accountSide(A.PartnerCost)).toBe("purchase");
    expect(accountSide(A.PartnerPayable)).toBe("purchase");
    expect(accountSide(A.VatInput)).toBe("purchase");
    expect(accountSide(A.Cash)).toBe("neutral");
    expect(accountSide(A.CollectionsReceivable)).toBe("neutral");
  });
});

describe("Both / Sales / Purchase narrow correctly (accountant sees all)", () => {
  it("Both shows everything (current full view)", () => {
    expect(accountsOf("both")).toEqual(allAccounts.map((a) => a.account));
  });

  it("Sales shows only sell-side accounts", () => {
    expect(accountsOf("sales")).toEqual([A.RevenueNetSell, A.FeeRevenue, A.MerchantReceivable, A.VatOutput]);
  });

  it("Purchase shows only buy-side accounts", () => {
    expect(accountsOf("purchase")).toEqual([A.PartnerCost, A.PartnerPayable, A.VatInput]);
  });

  it("margin + net VAT show only in Both (margin is platform-only)", () => {
    expect(marginVisible("both", PLATFORM)).toBe(true);
    expect(marginVisible("sales", PLATFORM)).toBe(false);
    expect(marginVisible("purchase", PLATFORM)).toBe(false);
    expect(netVatVisible("both")).toBe(true);
    expect(netVatVisible("sales")).toBe(false);
  });
});

describe("scope — accountant sees both", () => {
  it("PlatformAdmin + Accountant can see sales, purchase and margin", () => {
    for (const scope of [financeSideScope("PlatformAdmin"), financeSideScope("Accountant")]) {
      expect(scope.canSeeSales && scope.canSeePurchase && scope.canSeeMargin).toBe(true);
    }
  });
});

describe("scope — PARTNER is purchase-only (cannot surface sales / margin)", () => {
  it("forbidden sell-side fields are ABSENT even when 'Sales' is selected", () => {
    for (const side of ["both", "sales", "purchase"] as FinanceSide[]) {
      expect(accountVisible(A.RevenueNetSell, side, PARTNER)).toBe(false);
      expect(accountVisible(A.FeeRevenue, side, PARTNER)).toBe(false);
      expect(accountVisible(A.MerchantReceivable, side, PARTNER)).toBe(false);
      expect(marginVisible(side, PARTNER)).toBe(false);
      expect(salesVisible(side, PARTNER)).toBe(false);
    }
  });

  it("selecting Sales as a partner yields NO sell-side rows (their relationship is purchase)", () => {
    expect(accountsOf("sales", PARTNER)).toEqual([]);
  });

  it("partner still sees their own purchase/payable side", () => {
    expect(accountsOf("purchase", PARTNER)).toEqual([A.PartnerCost, A.PartnerPayable, A.VatInput]);
    expect(purchaseVisible("purchase", PARTNER)).toBe(true);
  });
});

describe("scope — MERCHANT is sales-only (cannot surface purchase / margin)", () => {
  it("forbidden buy-side fields are ABSENT even when 'Purchase' is selected", () => {
    for (const side of ["both", "sales", "purchase"] as FinanceSide[]) {
      expect(accountVisible(A.PartnerCost, side, MERCHANT)).toBe(false);
      expect(accountVisible(A.PartnerPayable, side, MERCHANT)).toBe(false);
      expect(marginVisible(side, MERCHANT)).toBe(false);
      expect(purchaseVisible(side, MERCHANT)).toBe(false);
    }
  });

  it("selecting Purchase as a merchant yields NO buy-side rows", () => {
    expect(accountsOf("purchase", MERCHANT)).toEqual([]);
  });

  it("merchant still sees their own sales/receivable side", () => {
    expect(accountsOf("sales", MERCHANT)).toEqual([A.RevenueNetSell, A.FeeRevenue, A.MerchantReceivable, A.VatOutput]);
  });
});

describe("never widens scope — output is always a subset, forbidden rows never appear", () => {
  it("every filtered row was in the input, for every role × side", () => {
    const full = new Set(allAccounts.map((a) => a.account));
    for (const scope of [PLATFORM, PARTNER, MERCHANT]) {
      for (const side of ["both", "sales", "purchase"] as FinanceSide[]) {
        for (const a of filterAccountsBySide(allAccounts, side, scope)) {
          expect(full.has(a.account)).toBe(true);
        }
      }
    }
  });

  it("a partner can never see a merchant-receivable row; a merchant can never see a partner-payable row", () => {
    for (const side of ["both", "sales", "purchase"] as FinanceSide[]) {
      expect(accountsOf(side, PARTNER)).not.toContain(A.MerchantReceivable);
      expect(accountsOf(side, MERCHANT)).not.toContain(A.PartnerPayable);
    }
  });
});

describe("canSeeContribution — platform/management + accountant ONLY", () => {
  it("PlatformAdmin and Accountant can see the platform Contribution / P&L", () => {
    expect(canSeeContribution(financeSideScope("PlatformAdmin"))).toBe(true);
    expect(canSeeContribution(financeSideScope("Accountant"))).toBe(true);
  });

  it("partner and merchant roles can NEVER see it (margin/both-side gate)", () => {
    expect(canSeeContribution(PARTNER)).toBe(false);
    expect(canSeeContribution(MERCHANT)).toBe(false);
    expect(canSeeContribution(financeSideScope("PartnerSuccessManager"))).toBe(false);
  });
});

describe("URL state + composition with the date-range filter", () => {
  it("reads side from ?side (default both)", () => {
    expect(financeSideFromParams(new URLSearchParams(""))).toBe("both");
    expect(financeSideFromParams(new URLSearchParams("side=sales"))).toBe("sales");
    expect(financeSideFromParams(new URLSearchParams("side=purchase"))).toBe("purchase");
    expect(financeSideFromParams(new URLSearchParams("side=bogus"))).toBe("both");
  });

  it("'both' clears the param (no behavior change when untouched)", () => {
    const p = applyFinanceSideToParams(new URLSearchParams("side=sales"), "both");
    expect(p.get("side")).toBeNull();
  });

  it("composes with the date range — both filters live in the same URL independently", () => {
    let p = new URLSearchParams();
    p = applyDateRangeToParams(p, { mode: "range", from: "2026-05-01", to: "2026-06-15" });
    p = applyFinanceSideToParams(p, "sales");
    // both read back correctly, neither clobbers the other
    expect(financeSideFromParams(p)).toBe("sales");
    expect(dateRangeFromParams(p)).toEqual({ mode: "range", from: "2026-05-01", to: "2026-06-15" });
    // changing the side leaves the date range intact
    p = applyFinanceSideToParams(p, "purchase");
    expect(dateRangeFromParams(p)).toEqual({ mode: "range", from: "2026-05-01", to: "2026-06-15" });
    expect(financeSideFromParams(p)).toBe("purchase");
  });
});
