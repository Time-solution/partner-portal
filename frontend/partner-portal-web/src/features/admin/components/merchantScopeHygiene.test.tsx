import { beforeEach, describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { renderToStaticMarkup } from "react-dom/server";
import {
  merchantOfferingView,
  toMerchantViewEntry,
  type MerchantBrowsePartnerEntry,
} from "@/lib/catalog/merchantBrowse";
import { merchantPackageDisplay } from "@/lib/usage/usagePackageDisplay";
import type { Partner, PartnerCatalogItem } from "@/lib/data/types";
import type { UsagePackage } from "@/lib/usage/usagePackage";
import { OfferingCard, OfferingDetailSheet, PartnerBrowseCard } from "./MerchantBrowseCards";
import { translations } from "@/lib/i18n";

function stubLocalStorage() {
  const bag = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => bag.get(k) ?? null,
    setItem: (k: string, v: string) => void bag.set(k, v),
    removeItem: (k: string) => void bag.delete(k),
    clear: () => bag.clear(),
  });
}

const sar = (amount: number) => ({ amount, currency: "SAR", vatInclusive: true });

const partner: Partner = {
  id: "22222222-2222-2222-2222-222222222004",
  legalName: "WhatsApp Co",
  tradeName: "WhatsApp Co",
  type: "Service",
  status: "Active",
  participationMode: "Principal",
  accentClass: "border-s-teal-500",
  partnerBrief: "مزود خدمات رسائل.",
} as Partner;

const item: PartnerCatalogItem = {
  id: "a1000003b-0003-4000-8000-00000000003b",
  partnerId: partner.id,
  code: "WA-PRO",
  name: "Pro tier",
  description: "خطة احترافية للرسائل.",
  merchantBenefit: "وصول أسرع لعملائك.",
  offeringKind: "ServiceSubscription",
  participationMode: "Principal",
  partnerCost: sar(49),
  settlementBook: "Integration",
  status: "Active",
};

const entry: MerchantBrowsePartnerEntry = {
  partner,
  moduleId: "subscriptions",
  offerings: [item],
  hasTierMenu: true,
};

const pkg: UsagePackage = {
  id: "pkg-1",
  partnerId: partner.id,
  name: "Messages 10k",
  unitLabel: "messages",
  mode: "Resale",
  currency: "SAR",
  includedQuantity: 10000,
  baseBuyAmount: 150,
  baseSellAmount: 250,
  overageBuyAmount: 0.03,
  overageSellAmount: 0.05,
  status: "Published",
  tiers: [],
} as unknown as UsagePackage;

/** Deep key walk — collects every key (at any depth) that smells like the buy leg or margin. */
function forbiddenKeysDeep(value: unknown, path = ""): string[] {
  if (!value || typeof value !== "object") return [];
  return Object.entries(value as Record<string, unknown>).flatMap(([key, child]) => {
    const hit = /partnercost|buy|cost|margin/i.test(key) ? [`${path}${key}`] : [];
    return [...hit, ...forbiddenKeysDeep(child, `${path}${key}.`)];
  });
}

describe("merchant prop hygiene — structural sweep (partnerCost/buy/cost/margin ABSENT)", () => {
  beforeEach(() => stubLocalStorage());

  it("MerchantOfferingView carries the merchant price only — no buy-leg keys at any depth", () => {
    const view = merchantOfferingView(item);
    expect(view.price.amount).toBe(100); // curated listed sell for this id — the resolved merchant price
    expect(forbiddenKeysDeep(view)).toEqual([]);
    expect("partnerCost" in view).toBe(false);
  });

  it("the full browse view entry (what PartnerBrowseCard receives) is clean, incl. the partner object", () => {
    const viewEntry = toMerchantViewEntry(entry);
    expect(forbiddenKeysDeep(viewEntry)).toEqual([]);
  });

  it("the merchant package display (what PackageDetailsCard receives) is clean", () => {
    expect(forbiddenKeysDeep(merchantPackageDisplay(pkg))).toEqual([]);
  });

  it("the exact ServiceOrderForm props the merchant page passes are clean (no buy prop exists)", () => {
    const view = merchantOfferingView(item);
    const props = {
      lang: "ar" as const,
      catalogItemId: view.id,
      offeringName: view.name,
      partnerId: view.partnerId,
      tenantId: "11111111-1111-1111-1111-111111111001",
      participationMode: view.participationMode === "SubscriptionFee" ? "SubscriptionFee" : "Principal",
      sellOrFee: view.price.amount,
      requirements: [],
      onDone: () => {},
      onCancel: () => {},
    };
    expect(forbiddenKeysDeep(props)).toEqual([]);
    expect("buy" in props).toBe(false);
  });
});

describe("scoped card rendering", () => {
  beforeEach(() => stubLocalStorage());

  it("OfferingCard shows the merchantOfferingPrice and never the item cost", () => {
    const view = merchantOfferingView(item);
    const html = renderToStaticMarkup(
      <OfferingCard lang="ar" offering={view} isActive={false} onDetails={() => {}} />,
    );
    expect(html).toContain("100.00"); // resolved merchant price (listed sell)
    expect(html).not.toContain("49.00"); // partner buy cost never rendered
    expect(html).toContain('data-testid="offering-kind-badge"');
    expect(html).toContain('data-testid="status-badge-Published"');
  });

  it("detail sheet shows price + VAT-inclusive note + activation-fee note + Activate CTA", () => {
    const viewEntry = toMerchantViewEntry(entry);
    const html = renderToStaticMarkup(
      <OfferingDetailSheet
        lang="ar"
        detail={{ entry: viewEntry, offering: viewEntry.offerings[0] }}
        isActive={false}
        busy={false}
        onClose={() => {}}
        onActivate={() => {}}
        onOrderService={() => {}}
      />,
    );
    expect(html).toContain('data-testid="sheet-price"');
    expect(html).toContain(translations.ar.browseVatNote);
    expect(html).toContain(translations.ar.browseActivationFeeLabel);
    expect(html).toContain('data-testid="sheet-activate"');
    expect(html).toContain('dir="rtl"');
    expect(html).not.toContain("49.00");
  });

  it("L1 partner card carries type badge + offering count + L2 grid", () => {
    const viewEntry = toMerchantViewEntry(entry);
    const html = renderToStaticMarkup(
      <PartnerBrowseCard
        entry={viewEntry}
        lang="ar"
        activeCatalogIds={new Set()}
        onViewDetails={() => {}}
        packages={[merchantPackageDisplay(pkg)]}
      />,
    );
    expect(html).toContain('data-testid="partner-type-badge"');
    expect(html).toContain(translations.ar.browseOfferingCount);
    expect(html).toContain(`data-testid="offering-card-${item.id}"`);
    expect(html).not.toContain("150.00"); // package buy never rendered
  });
});

describe("dialog copy + error-state wiring (page source contract)", () => {
  const read = (rel: string) => readFileSync(join(__dirname, rel), "utf8");

  it("deactivate dialog states BOTH facts: proration + re-activation allowed", () => {
    for (const lang of ["ar", "en"] as const) {
      expect(translations[lang].deactivateProrationSentence.length).toBeGreaterThan(10);
      expect(translations[lang].deactivateReactivateSentence.length).toBeGreaterThan(10);
    }
    const page = read("../pages/MerchantPreviewPage.tsx");
    const dialogBlock = page.slice(page.indexOf('testId="deactivate-confirm"'));
    expect(dialogBlock).toContain("deactivateProrationSentence");
    expect(dialogBlock).toContain("deactivateReactivateSentence");
  });

  it("activate confirm dialog carries name + price + fee + starts-now", () => {
    const page = read("../pages/MerchantPreviewPage.tsx");
    const block = page.slice(page.indexOf('testId="activate-confirm"'));
    expect(block).toContain("activateConfirmPriceLabel");
    expect(block).toContain("browseActivationFeeLabel");
    expect(block).toContain("activateConfirmStartsNow");
  });

  it("success toast offers the view-in-my-services jump (dead end killed)", () => {
    const page = read("../pages/MerchantPreviewPage.tsx");
    expect(page).toContain("toast-view-services");
    expect(page).toContain('setTab("services")');
  });

  it("ERROR states are wired on the 3 previously-missing pages", () => {
    for (const rel of ["../pages/CatalogPage.tsx", "../pages/UsagePackagesPage.tsx", "../pages/ActivationsPage.tsx"]) {
      const src = read(rel);
      expect(src).toContain("<ErrorState");
      expect(src).toContain("stateErrorGeneric");
      expect(src).toContain("stateRetry");
    }
  });
});
