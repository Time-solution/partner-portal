import { describe, expect, it } from "vitest";
import { createSeedData } from "@/lib/data/fixtures";
import { deriveInvoicePayment } from "@/lib/data/types";
import { MOCK_MERCHANT_PREVIEW } from "@/lib/mock/merchantPreview";
import {
  assertMerchantRowFieldScope,
  MERCHANT_ACTIVE_PARTNER_FORBIDDEN_KEYS,
} from "@/lib/dashboard/merchantActivePartners";
import { buildMerchantRollupStatement } from "./merchantRollupStatement";

const PARTNER_WHATSAPP = "22222222-2222-2222-2222-222222222004";

describe("buildMerchantRollupStatement — SALES-side cross-partner roll-up", () => {
  const data = createSeedData();
  const partnerNameById = new Map(data.partners.map((p) => [p.id, p.tradeName ?? p.legalName]));

  it("rolls up ALL of a multi-partner merchant's partners as separate lines (not merged)", () => {
    const stmt = buildMerchantRollupStatement(data, MOCK_MERCHANT_PREVIEW.tenantId, partnerNameById);
    // Burger Co: Chefz + Salasa + W there → 3 separate lines, NOT one merged invoice.
    expect(stmt.lineCount).toBe(3);
    expect(stmt.lines.some((l) => l.partnerName.includes("Salasa"))).toBe(true);
    expect(stmt.lines.some((l) => l.partnerName.includes("Chefz"))).toBe(true);
    expect(stmt.lines.some((l) => l.partnerName.includes("W there"))).toBe(true);
  });

  it("combined totals = sum of the per-partner read-model figures (W there 119 only)", () => {
    const stmt = buildMerchantRollupStatement(data, MOCK_MERCHANT_PREVIEW.tenantId, partnerNameById);
    const wthere = stmt.lines.find((l) => l.partnerName.includes("W there"))!;
    expect(wthere.billed).toBe(119);
    expect(wthere.payment.paidToDate).toBe(119);
    expect(wthere.payment.remaining).toBe(0);
    // Only W there has a subscription invoice — Salasa (Principal) / Chefz (ReflectionOnly) bill 0.
    expect(stmt.totals).toEqual({ billed: 119, paid: 119, remaining: 0 });
  });

  it("ties out to deriveInvoicePayment over the merchant's billing periods (no recompute)", () => {
    const periods = data.billingPeriods.filter((p) => p.tenantId === MOCK_MERCHANT_PREVIEW.tenantId);
    let paid = 0;
    let outstanding = 0;
    for (const p of periods) {
      const s = deriveInvoicePayment(p);
      paid += s.amountPaid;
      outstanding += s.amountOutstanding;
    }
    const stmt = buildMerchantRollupStatement(data, MOCK_MERCHANT_PREVIEW.tenantId, partnerNameById);
    expect(stmt.totals.paid).toBe(Math.round(paid * 100) / 100);
    expect(stmt.totals.remaining).toBe(Math.round(outstanding * 100) / 100);
  });

  it("buy / cost / margin fields are ABSENT from every line (merchant SALES scope)", () => {
    const stmt = buildMerchantRollupStatement(data, MOCK_MERCHANT_PREVIEW.tenantId, partnerNameById);
    for (const line of stmt.lines) {
      assertMerchantRowFieldScope(line);
      const raw = line as unknown as Record<string, unknown>;
      for (const key of MERCHANT_ACTIVE_PARTNER_FORBIDDEN_KEYS) {
        expect(raw).not.toHaveProperty(key);
      }
    }
  });

  it("honors useDateRange: out-of-range period keeps the roster but zeroes invoiced figures", () => {
    const stmt = buildMerchantRollupStatement(
      data,
      MOCK_MERCHANT_PREVIEW.tenantId,
      partnerNameById,
      { mode: "period", period: "2099-01" },
    );
    expect(stmt.lineCount).toBe(3); // active relationships still listed
    expect(stmt.totals).toEqual({ billed: 0, paid: 0, remaining: 0 }); // no in-range invoice
  });

  it("period filter that DOES include the invoice month shows the figure (compose-ready)", () => {
    const stmt = buildMerchantRollupStatement(
      data,
      MOCK_MERCHANT_PREVIEW.tenantId,
      partnerNameById,
      { mode: "period", period: "2026-06" },
    );
    expect(stmt.totals.billed).toBe(119);
  });

  it("scopes to the requested merchant only (never widens)", () => {
    const stmt = buildMerchantRollupStatement(data, MOCK_MERCHANT_PREVIEW.tenantId, partnerNameById);
    expect(stmt.lines.some((l) => l.partnerId === PARTNER_WHATSAPP)).toBe(false);
  });
});
