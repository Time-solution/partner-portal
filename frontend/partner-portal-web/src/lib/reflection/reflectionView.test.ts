import { describe, expect, it } from "vitest";
import { mockPortalData } from "@/lib/data/fixtures";
import { translations, type Lang } from "@/lib/i18n";
import {
  PARTNER_ONLY_I18N_KEYS,
  buildReflectionDetailText,
  filterMerchantReflectionOrders,
  reflectionDetailLabelKeys,
} from "./reflectionView";

const TENANT_DEMO = "11111111-1111-1111-1111-111111111099";
const PARTNER_CHEFZ = "22222222-2222-2222-2222-222222222006";

function t(lang: Lang) {
  return (key: string) => translations[lang][key] ?? key;
}

function chefzDemoOrder() {
  const order = mockPortalData.reflectedOrders.find((o) => o.id === "ref-chefz-demo");
  if (!order?.reflection) throw new Error("ref-chefz-demo fixture missing");
  return order;
}

describe("filterMerchantReflectionOrders", () => {
  it("returns only reflection rows for the requested tenant", () => {
    const scoped = filterMerchantReflectionOrders(mockPortalData.reflectedOrders, TENANT_DEMO);
    expect(scoped.length).toBeGreaterThan(0);
    expect(scoped.every((o) => o.tenantId === TENANT_DEMO)).toBe(true);
    expect(scoped.every((o) => o.reflection != null)).toBe(true);
  });

  it("does not include other tenants' Chefz reflections", () => {
    const scoped = filterMerchantReflectionOrders(mockPortalData.reflectedOrders, TENANT_DEMO);
    expect(scoped.some((o) => o.partnerId === PARTNER_CHEFZ)).toBe(true);
    expect(scoped.some((o) => o.id === "ref-chefz-1")).toBe(false);
  });
});

describe("merchant reflection detail scoping", () => {
  const order = chefzDemoOrder();

  it("shows item, merchant price, via Chefz, and service type", () => {
    const text = buildReflectionDetailText(order, "merchant", "en", t("en"));
    expect(text).toContain("10.00");
    expect(text).toContain("Via Chefz");
    expect(text).toContain("Order reflection (aggregator)");
    expect(text).toContain(order.orderLineId);
  });

  it("hides partner list price, delivery, customer paid, and Zahy fee labels and amounts", () => {
    const text = buildReflectionDetailText(order, "merchant", "en", t("en"));
    for (const key of PARTNER_ONLY_I18N_KEYS) {
      expect(text).not.toContain(translations.en[key]);
    }
    expect(text).not.toContain("13.00");
    expect(text).not.toContain("23.00");
    expect(text).not.toContain("1.00");
  });

  it("uses Arabic via-partner copy in RTL locale", () => {
    const text = buildReflectionDetailText(order, "merchant", "ar", t("ar"));
    expect(text).toContain("عبر Chefz");
    expect(text).toContain("نوع الخدمة");
    expect(text).toContain("السعر");
  });

  it("merchant label keys exclude partner-only economics", () => {
    const merchantKeys = reflectionDetailLabelKeys("merchant");
    for (const key of PARTNER_ONLY_I18N_KEYS) {
      expect(merchantKeys).not.toContain(key);
    }
  });
});

describe("partner/admin full reflection detail (unchanged)", () => {
  const order = chefzDemoOrder();

  it("still shows the full economics breakdown", () => {
    const text = buildReflectionDetailText(order, "full", "en", t("en"));
    expect(text).toContain("10.00");
    expect(text).toContain("13.00");
    expect(text).toContain("23.00");
    expect(text).toContain(translations.en.reflListPrice);
    expect(text).toContain(translations.en.reflDeliveryFee);
    expect(text).toContain(translations.en.reflCustomerPaid);
    expect(text).toContain(translations.en.reflZahyFee);
  });

  it("full viewer includes all partner-only label keys", () => {
    const keys = reflectionDetailLabelKeys("full", true);
    for (const key of PARTNER_ONLY_I18N_KEYS) {
      expect(keys).toContain(key);
    }
  });
});
