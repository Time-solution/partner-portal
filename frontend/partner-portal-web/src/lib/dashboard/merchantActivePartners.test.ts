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

const TENANT_PIZZA = "11111111-1111-1111-1111-111111111003";
const PARTNER_WHATSAPP = "22222222-2222-2222-2222-222222222004";

describe("buildMerchantActivePartners", () => {
  const data = createSeedData();
  const partnerNameById = new Map(
    data.partners.map((p) => [p.id, p.tradeName ?? p.legalName]),
  );

  it("shows only this merchant's activations", () => {
    const rows = buildMerchantActivePartners(data, TENANT_PIZZA, partnerNameById);
    expect(rows.length).toBe(2);
    expect(rows.some((r) => r.partnerName.includes("WhatsApp"))).toBe(true);
    expect(rows.some((r) => r.partnerName.includes("Oto"))).toBe(true);
    const demoRows = buildMerchantActivePartners(
      data,
      MOCK_MERCHANT_PREVIEW.tenantId,
      partnerNameById,
    );
    expect(demoRows.some((r) => r.partnerId === PARTNER_WHATSAPP)).toBe(false);
    expect(demoRows.some((r) => r.partnerName.includes("Salasa"))).toBe(true);
    expect(demoRows.some((r) => r.partnerName.includes("Chefz"))).toBe(true);
    const pizzaRows = buildMerchantActivePartners(data, TENANT_PIZZA, partnerNameById);
    expect(pizzaRows.some((r) => r.partnerId === PARTNER_WHATSAPP)).toBe(true);
  });

  it("forbidden buy/margin keys are absent from rows (not hidden)", () => {
    const rows = buildMerchantActivePartners(data, TENANT_PIZZA, partnerNameById);
    for (const row of rows) {
      assertMerchantRowFieldScope(row);
      const raw = row as unknown as Record<string, unknown>;
      for (const key of MERCHANT_ACTIVE_PARTNER_FORBIDDEN_KEYS) {
        expect(raw).not.toHaveProperty(key);
      }
    }
  });

  it("includes merchant sell price only", () => {
    const rows = buildMerchantActivePartners(data, TENANT_PIZZA, partnerNameById);
    const whatsapp = rows.find((r) => r.partnerId === PARTNER_WHATSAPP);
    expect(whatsapp!.sellPrice.amount).toBe(100);
    expect(whatsapp).not.toHaveProperty("buyPrice");
    expect(whatsapp).not.toHaveProperty("payable");
  });

  it("payment remaining ties to deriveInvoicePayment read model", () => {
    const activation = data.activations.find(
      (a) => a.tenantId === TENANT_PIZZA && a.partnerId === PARTNER_WHATSAPP,
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

    const rows = buildMerchantActivePartners(data, TENANT_PIZZA, partnerNameById);
    const row = rows.find((r) => r.partnerId === PARTNER_WHATSAPP);
    expect(row!.payment.paidToDate).toBe(payment.paidToDate);
    expect(row!.payment.remaining).toBe(payment.remaining);
  });

  it("demo merchant (Burger Co) shows multi-partner activations without subscription invoice slice", () => {
    const rows = buildMerchantActivePartners(
      data,
      MOCK_MERCHANT_PREVIEW.tenantId,
      partnerNameById,
    );
    expect(rows.length).toBe(3);
    expect(rows.some((r) => r.partnerName.includes("Salasa"))).toBe(true);
    expect(rows.some((r) => r.partnerName.includes("Chefz"))).toBe(true);
    expect(rows.some((r) => r.partnerName.includes("W there"))).toBe(true);
    const wthere = rows.find((r) => r.partnerName.includes("W there"));
    expect(wthere!.payment.paidToDate).toBe(119);
    expect(wthere!.payment.remaining).toBe(0);
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
