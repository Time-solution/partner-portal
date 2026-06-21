import { describe, expect, it } from "vitest";
import { createSeedData } from "@/lib/data/fixtures";
import { deriveInvoicePayment } from "@/lib/data/types";
import { MOCK_MERCHANT_PREVIEW } from "@/lib/mock/merchantPreview";
import {
  assertMerchantRowFieldScope,
  buildMerchantActivePartners,
  MERCHANT_ACTIVE_PARTNER_FORBIDDEN_KEYS,
  sumActivationInvoicePayment,
} from "./merchantActivePartners";
import { defaultLandingPath, isPathAllowedForRole } from "@/lib/rbac/roleNavConfig";

const TENANT_QUICKBITES = "11111111-1111-1111-1111-111111111003";
const PARTNER_JAHEZ = "22222222-2222-2222-2222-222222222004";

describe("buildMerchantActivePartners", () => {
  const data = createSeedData();
  const partnerNameById = new Map(
    data.partners.map((p) => [p.id, p.tradeName ?? p.legalName]),
  );

  it("shows only this merchant's activations", () => {
    const rows = buildMerchantActivePartners(data, TENANT_QUICKBITES, partnerNameById);
    expect(rows.length).toBeGreaterThan(0);
    expect(rows.every((r) => r.partnerName.includes("Jahez") || r.partnerId === PARTNER_JAHEZ)).toBe(
      true,
    );
    const demoRows = buildMerchantActivePartners(
      data,
      MOCK_MERCHANT_PREVIEW.tenantId,
      partnerNameById,
    );
    expect(demoRows.some((r) => r.partnerId === PARTNER_JAHEZ)).toBe(true);
    const quickBitesRows = buildMerchantActivePartners(data, TENANT_QUICKBITES, partnerNameById);
    expect(quickBitesRows.some((r) => r.partnerId === PARTNER_JAHEZ)).toBe(true);
    expect(demoRows.some((r) => r.partnerName.includes("Jahez"))).toBe(true);
  });

  it("forbidden buy/margin keys are absent from rows (not hidden)", () => {
    const rows = buildMerchantActivePartners(data, TENANT_QUICKBITES, partnerNameById);
    for (const row of rows) {
      assertMerchantRowFieldScope(row);
      const raw = row as unknown as Record<string, unknown>;
      for (const key of MERCHANT_ACTIVE_PARTNER_FORBIDDEN_KEYS) {
        expect(raw).not.toHaveProperty(key);
      }
    }
  });

  it("includes merchant sell price only", () => {
    const rows = buildMerchantActivePartners(data, TENANT_QUICKBITES, partnerNameById);
    const row = rows[0];
    expect(row.sellPrice.amount).toBe(115);
    expect(row).not.toHaveProperty("buyPrice");
    expect(row).not.toHaveProperty("payable");
  });

  it("payment remaining ties to deriveInvoicePayment read model", () => {
    const activation = data.activations.find(
      (a) => a.tenantId === TENANT_QUICKBITES && a.partnerId === PARTNER_JAHEZ,
    )!;
    const payment = sumActivationInvoicePayment(data, activation);
    const periods = data.billingPeriods.filter(
      (p) => p.partnerId === activation.partnerId && p.tenantId === activation.tenantId,
    );
    let expectedPaid = 0;
    let expectedRemaining = 0;
    for (const p of periods) {
      const state = deriveInvoicePayment(p);
      expectedPaid += state.amountPaid;
      expectedRemaining += state.amountOutstanding;
    }
    expect(payment.paidToDate).toBe(Math.round(expectedPaid * 100) / 100);
    expect(payment.remaining).toBe(Math.round(expectedRemaining * 100) / 100);

    const rows = buildMerchantActivePartners(data, TENANT_QUICKBITES, partnerNameById);
    const row = rows.find((r) => r.partnerId === PARTNER_JAHEZ);
    expect(row!.payment.paidToDate).toBe(payment.paidToDate);
    expect(row!.payment.remaining).toBe(payment.remaining);
  });

  it("demo merchant activation shows partial payment from invoice read model", () => {
    const rows = buildMerchantActivePartners(
      data,
      MOCK_MERCHANT_PREVIEW.tenantId,
      partnerNameById,
    );
    const jahez = rows.find((r) => r.partnerId === PARTNER_JAHEZ);
    expect(jahez).toBeDefined();
    expect(jahez!.payment.paidToDate).toBe(50);
    expect(jahez!.payment.remaining).toBe(65);
  });
});

describe("merchant dashboard RBAC paths", () => {
  it("merchant landing defaults to partners tab", () => {
    expect(defaultLandingPath("MerchantPreview")).toBe("/merchant-preview?tab=partners");
  });

  it("blocks merchant from partner and finance URLs", () => {
    expect(isPathAllowedForRole("/partners/22222222-2222-2222-2222-222222222001/active-merchants", "MerchantPreview")).toBe(false);
    expect(isPathAllowedForRole("/finance/reports", "MerchantPreview")).toBe(false);
    expect(isPathAllowedForRole("/merchant-preview", "MerchantPreview")).toBe(true);
  });
});
