import { describe, expect, it } from "vitest";
import { createSeedData } from "@/lib/data/fixtures";
import {
  assertPartnerRowFieldScope,
  buildPartnerActiveMerchants,
  PARTNER_ACTIVE_MERCHANT_FORBIDDEN_KEYS,
} from "@/lib/dashboard/partnerActiveMerchants";
import { buildPartnerRollupStatement } from "./partnerRollupStatement";

const PARTNER_SALASA = "22222222-2222-2222-2222-222222222001"; // carrier — Zahy owes delivery cost

describe("buildPartnerRollupStatement — PURCHASE/payable-side statement (Direction 2)", () => {
  const data = createSeedData();

  it("lists own active merchants + owed/disbursed/remaining, tied to the source builder", () => {
    const stmt = buildPartnerRollupStatement(data, PARTNER_SALASA);
    const source = buildPartnerActiveMerchants(data, PARTNER_SALASA);
    expect(stmt.lineCount).toBe(source.rows.length);
    expect(stmt.lineCount).toBeGreaterThan(0);
    expect(stmt.totals.owed).toBe(source.partnerPayableTotal);
    expect(stmt.totals.owed).toBeGreaterThan(0); // carrier => Zahy owes a payable
    expect(stmt.totals.disbursed).toBe(0); // mock has no disbursement ledger
    expect(stmt.totals.remaining).toBe(stmt.totals.owed);
  });

  it("sell / resale / margin / receivable fields are ABSENT from every line (partner PURCHASE scope)", () => {
    const stmt = buildPartnerRollupStatement(data, PARTNER_SALASA);
    for (const line of stmt.lines) {
      assertPartnerRowFieldScope(line);
      const raw = line as unknown as Record<string, unknown>;
      for (const key of PARTNER_ACTIVE_MERCHANT_FORBIDDEN_KEYS) {
        expect(raw).not.toHaveProperty(key);
      }
    }
  });

  it("honors useDateRange: out-of-range period keeps the merchant roster but zeroes the payable", () => {
    const all = buildPartnerRollupStatement(data, PARTNER_SALASA);
    const future = buildPartnerRollupStatement(data, PARTNER_SALASA, { mode: "period", period: "2099-01" });
    expect(future.lineCount).toBe(all.lineCount); // active merchants still listed
    expect(future.totals.owed).toBe(0); // no in-range settlement activity
    expect(future.totals.remaining).toBe(0);
  });

  it("never widens scope — only the requested partner's merchants appear", () => {
    const stmt = buildPartnerRollupStatement(data, PARTNER_SALASA);
    const sourceTenants = new Set(
      data.activations.filter((a) => a.partnerId === PARTNER_SALASA).map((a) => a.tenantId),
    );
    for (const line of stmt.lines) {
      expect(sourceTenants.has(line.tenantId)).toBe(true);
    }
  });
});
