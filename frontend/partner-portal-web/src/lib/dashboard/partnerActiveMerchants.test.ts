import { describe, expect, it } from "vitest";
import { createSeedData } from "@/lib/data/fixtures";
import {
  assertPartnerRowFieldScope,
  buildPartnerActiveMerchants,
  PARTNER_ACTIVE_MERCHANT_FORBIDDEN_KEYS,
  sumMerchantPartnerPayable,
} from "./partnerActiveMerchants";
import { partnerStatement, toReportEntries } from "@/lib/reports/settlementReports";
import {
  defaultLandingPath,
  isPathAllowedForRole,
  MOCK_PARTNER_FINANCE_PARTNER_ID,
  MOCK_PARTNER_PSM_PARTNER_ID,
  partnerHomePath,
} from "@/lib/rbac/roleNavConfig";

const PARTNER_SALASA = "22222222-2222-2222-2222-222222222001";
const PARTNER_WHATSAPP = "22222222-2222-2222-2222-222222222004";
const TENANT_BURGER = "11111111-1111-1111-1111-111111111001";

describe("buildPartnerActiveMerchants", () => {
  const data = createSeedData();

  it("shows only this partner's active merchants", () => {
    const view = buildPartnerActiveMerchants(data, PARTNER_SALASA);
    expect(view.rows.every((r) => r.merchantName)).toBe(true);
    expect(view.rows.some((r) => r.merchantName === "Burger Co")).toBe(true);
    expect(view.rows.some((r) => r.merchantName === "Pizza House")).toBe(false);
    expect(view.rows.every((r) => r.status === "Active")).toBe(true);
  });

  it("excludes another partner's merchants entirely", () => {
    const salasa = buildPartnerActiveMerchants(data, PARTNER_SALASA);
    const whatsapp = buildPartnerActiveMerchants(data, PARTNER_WHATSAPP);
    const salasaTenants = new Set(salasa.rows.map((r) => r.tenantId));
    const whatsappTenants = new Set(whatsapp.rows.map((r) => r.tenantId));
    for (const t of whatsappTenants) {
      expect(salasaTenants.has(t)).toBe(false);
    }
    expect(whatsapp.rows.some((r) => r.merchantName === "Pizza House")).toBe(true);
  });

  it("forbidden sell/margin keys are absent from rows (not hidden)", () => {
    const view = buildPartnerActiveMerchants(data, PARTNER_SALASA);
    for (const row of view.rows) {
      assertPartnerRowFieldScope(row);
      const raw = row as unknown as Record<string, unknown>;
      for (const key of PARTNER_ACTIVE_MERCHANT_FORBIDDEN_KEYS) {
        expect(raw).not.toHaveProperty(key);
      }
    }
  });

  it("merchant payable ties to PartnerStatement entry slice — no recompute", () => {
    const entries = toReportEntries(data);
    const statement = partnerStatement(entries, PARTNER_SALASA);
    const view = buildPartnerActiveMerchants(data, PARTNER_SALASA);
    const burger = view.rows.find((r) => r.tenantId === TENANT_BURGER);
    expect(burger).toBeDefined();
    const expected = sumMerchantPartnerPayable(statement.entries, TENANT_BURGER);
    expect(burger!.payable.owed).toBe(expected);
    expect(burger!.payable.disbursed).toBe(0);
    expect(burger!.payable.remainingToDisburse).toBe(expected);
    expect(view.partnerPayableTotal).toBe(statement.payable);
  });
});

describe("partner dashboard RBAC paths", () => {
  it("partner landing is active-merchants scoped path", () => {
    expect(defaultLandingPath("PartnerSuccessManager")).toBe(
      partnerHomePath(MOCK_PARTNER_PSM_PARTNER_ID),
    );
    expect(defaultLandingPath("PartnerFinance")).toBe(
      partnerHomePath(MOCK_PARTNER_FINANCE_PARTNER_ID),
    );
  });

  it("blocks direct URL to another partner active-merchants", () => {
    const chefz = "22222222-2222-2222-2222-222222222006";
    expect(
      isPathAllowedForRole(
        `/partners/${MOCK_PARTNER_FINANCE_PARTNER_ID}/active-merchants`,
        "PartnerFinance",
        chefz,
      ),
    ).toBe(false);
    expect(
      isPathAllowedForRole(
        `/partners/${chefz}/active-merchants`,
        "PartnerFinance",
        chefz,
      ),
    ).toBe(true);
  });
});
