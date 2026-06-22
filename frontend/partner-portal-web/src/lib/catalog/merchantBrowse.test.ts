import { beforeEach, describe, expect, it, vi } from "vitest";
import { forbiddenPriceKeys, projectCatalogPrice } from "./catalogPriceVisibility";
import {
  buildMerchantActivationIdempotencyKey,
  buildMerchantBrowseGroups,
  merchantListedSellPrice,
  partnerHasTierMenu,
} from "./merchantBrowse";
import type { Partner, PartnerCatalogItem } from "@/lib/data/types";

const sar = (amount: number) => ({ amount, currency: "SAR", vatInclusive: true });

beforeEach(() => {
  const mem = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => mem.get(k) ?? null,
    setItem: (k: string, v: string) => {
      mem.set(k, v);
    },
    removeItem: (k: string) => {
      mem.delete(k);
    },
  });
});

const deliveryPartner: Partner = {
  id: "p-del",
  legalName: "Salasa",
  type: "Carrier",
  status: "Active",
  primaryContactEmail: "a@b.sa",
  participationMode: "Principal",
  accentClass: "border-l-emerald-500",
};

const servicePartner: Partner = {
  id: "p-svc",
  legalName: "Jahez",
  type: "Service",
  status: "Active",
  primaryContactEmail: "a@b.sa",
  participationMode: "SubscriptionFee",
  accentClass: "border-l-sky-500",
};

const inactivePartner: Partner = {
  id: "p-off",
  legalName: "Closed",
  type: "Carrier",
  status: "Suspended",
  primaryContactEmail: "a@b.sa",
  participationMode: "Principal",
  accentClass: "border-l-gray-400",
};

const deliveryItem: PartnerCatalogItem = {
  id: "a1000001-0001-4000-8000-000000000001",
  partnerId: "p-del",
  code: "DEL-STD",
  name: "Delivery",
  offeringKind: "DeliveryFulfilmentPerOrder",
  participationMode: "Principal",
  partnerCost: sar(10),
  settlementBook: "Marketplace",
  status: "Active",
};

const tierBasic: PartnerCatalogItem = {
  id: "a1000003-0003-4000-8000-000000000003",
  partnerId: "p-svc",
  code: "SVC-BASIC",
  name: "Basic",
  offeringKind: "ServiceOneOff",
  participationMode: "SubscriptionFee",
  partnerCost: sar(49),
  settlementBook: "Integration",
  status: "Active",
};

const tierPro: PartnerCatalogItem = {
  id: "a1000003b-0003-4000-8000-00000000003b",
  partnerId: "p-svc",
  code: "SVC-PRO",
  name: "Pro",
  offeringKind: "ServiceSubscription",
  participationMode: "SubscriptionFee",
  partnerCost: sar(70),
  settlementBook: "Integration",
  status: "Active",
};

describe("buildMerchantBrowseGroups", () => {
  it("groups active partners by category and excludes inactive", () => {
    const groups = buildMerchantBrowseGroups(
      [deliveryPartner, servicePartner, inactivePartner],
      [deliveryItem, tierBasic, tierPro],
    );
    const allPartnerIds = groups.flatMap((g) => g.partners.map((p) => p.partner.id));
    expect(allPartnerIds).toContain("p-del");
    expect(allPartnerIds).toContain("p-svc");
    expect(allPartnerIds).not.toContain("p-off");
    expect(groups.some((g) => g.moduleId === "delivery-service")).toBe(true);
    expect(groups.some((g) => g.moduleId === "subscriptions")).toBe(true);
  });

  it("service partner has tier menu with multiple offerings", () => {
    const groups = buildMerchantBrowseGroups([servicePartner], [tierBasic, tierPro]);
    const entry = groups.flatMap((g) => g.partners).find((p) => p.partner.id === "p-svc");
    expect(entry?.hasTierMenu).toBe(true);
    expect(entry?.offerings).toHaveLength(2);
    expect(partnerHasTierMenu(servicePartner, [tierBasic, tierPro])).toBe(true);
  });

  it("delivery partner has no tier menu", () => {
    expect(partnerHasTierMenu(deliveryPartner, [deliveryItem])).toBe(false);
  });
});

describe("merchantListedSellPrice + scoped visibility", () => {
  it("delivery merchant render has NO buy or margin", () => {
    const sell = merchantListedSellPrice(deliveryItem);
    const scoped = projectCatalogPrice({ buy: deliveryItem.partnerCost, sell }, "merchant");
    for (const key of forbiddenPriceKeys("merchant")) {
      expect(scoped).not.toHaveProperty(key);
    }
    expect(scoped.sell?.amount).toBe(13);
  });
});

describe("buildMerchantActivationIdempotencyKey", () => {
  it("matches backend activation idempotency shape", () => {
    expect(
      buildMerchantActivationIdempotencyKey(
        "11111111-1111-1111-1111-111111111001",
        "a1000003-0003-4000-8000-000000000003",
      ),
    ).toBe(
      "activation:11111111-1111-1111-1111-111111111001:a1000003-0003-4000-8000-000000000003",
    );
  });
});

describe("instant activation mock contract", () => {
  it("idempotent key is per catalog item not per partner", () => {
    const tenant = "11111111-1111-1111-1111-111111111001";
    const k1 = buildMerchantActivationIdempotencyKey(tenant, tierBasic.id);
    const k2 = buildMerchantActivationIdempotencyKey(tenant, tierPro.id);
    expect(k1).not.toBe(k2);
  });

  it("createActivation is instant Active and idempotent per tier", async () => {
    const { MockPortalDataSource } = await import("@/lib/data/mockDataSource");
    const { MOCK_MERCHANT_PREVIEW } = await import("@/lib/mock/merchantPreview");
    const ds = new MockPortalDataSource();
    const catalogId = "a1000003b-0003-4000-8000-00000000003b";
    const partnerId = "22222222-2222-2222-2222-222222222004";

    const first = await ds.createActivation({
      partnerId,
      catalogItemId: catalogId,
      tenantId: MOCK_MERCHANT_PREVIEW.tenantId,
      merchantName: MOCK_MERCHANT_PREVIEW.merchantName,
    });
    expect(first.activation.status).toBe("Active");
    expect(first.workflow.stage).toBe("Active");

    const second = await ds.createActivation({
      partnerId,
      catalogItemId: catalogId,
      tenantId: MOCK_MERCHANT_PREVIEW.tenantId,
      merchantName: MOCK_MERCHANT_PREVIEW.merchantName,
    });
    expect(second.activation.id).toBe(first.activation.id);

    const activations = await ds.getActivations();
    const mine = activations.filter(
      (r) =>
        r.activation.tenantId === MOCK_MERCHANT_PREVIEW.tenantId &&
        r.activation.catalogItemId === catalogId &&
        r.activation.status !== "Ended",
    );
    expect(mine).toHaveLength(1);
  });
});
