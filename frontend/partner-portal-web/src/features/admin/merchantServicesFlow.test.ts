import { beforeEach, describe, expect, it, vi } from "vitest";
import { MockPortalDataSource } from "@/lib/data/mockDataSource";
import { buildMerchantActivePartners } from "@/lib/dashboard/merchantActivePartners";
import { MOCK_MERCHANT_PREVIEW } from "@/lib/mock/merchantPreview";

function stubLocalStorage() {
  const bag = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => bag.get(k) ?? null,
    setItem: (k: string, v: string) => void bag.set(k, v),
    removeItem: (k: string) => void bag.delete(k),
    clear: () => bag.clear(),
  });
}

const CATALOG_ID = "a1000003b-0003-4000-8000-00000000003b";
const PARTNER_ID = "22222222-2222-2222-2222-222222222004";
const input = () => ({
  partnerId: PARTNER_ID,
  catalogItemId: CATALOG_ID,
  tenantId: MOCK_MERCHANT_PREVIEW.tenantId,
  merchantName: MOCK_MERCHANT_PREVIEW.merchantName,
});

describe("merchant services flow (activate → my services → end → re-activate)", () => {
  beforeEach(() => stubLocalStorage());

  it("activation appears as an Active card row in My Services", async () => {
    const ds = new MockPortalDataSource();
    const created = await ds.createActivation(input());
    expect(created.activation.status).toBe("Active");

    const data = await ds.getAll();
    const rows = buildMerchantActivePartners(
      data,
      MOCK_MERCHANT_PREVIEW.tenantId,
      new Map(data.partners.map((p) => [p.id, p.tradeName ?? p.legalName])),
    );
    const mine = rows.find((r) => r.activationId === created.activation.id);
    expect(mine).toBeDefined();
    expect(mine!.status).toBe("Active");
    // Merchant row scope: the sell price shows; walk guards live in merchantActivePartners itself.
    expect(mine!.sellPrice.amount).toBeGreaterThan(0);
  });

  it("end → re-activate renders a FRESH Active row while the ended row stays visible", async () => {
    const ds = new MockPortalDataSource();
    const first = await ds.createActivation(input());
    await ds.endActivation(first.activation.id);
    const second = await ds.createActivation(input());
    expect(second.activation.id).not.toBe(first.activation.id);

    const data = await ds.getAll();
    // includeEnded — the SAME opt-in My Services uses to keep the ended history visible.
    const rows = buildMerchantActivePartners(
      data,
      MOCK_MERCHANT_PREVIEW.tenantId,
      new Map(data.partners.map((p) => [p.id, p.tradeName ?? p.legalName])),
      new Date(),
      { includeEnded: true },
    );
    const endedRow = rows.find((r) => r.activationId === first.activation.id);
    const freshRow = rows.find((r) => r.activationId === second.activation.id);
    expect(endedRow?.status).toBe("Ended"); // history preserved (rendered muted, no End action)
    expect(freshRow?.status).toBe("Active"); // the re-activation is a brand-new card
  });
});
