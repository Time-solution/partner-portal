import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import {
  canAuthorOfferingPresentation,
  resolveCatalogAuthoringMode,
} from "@/lib/catalog/catalogAuthoring";
import { emptyListing, type OfferingListing } from "@/lib/catalog/listingSchema";
import { getListing, resetListings } from "@/lib/catalog/listingStore";
import type { PartnerCatalogItem } from "@/lib/data/types";
import type { UsagePackage } from "@/lib/usage/usagePackage";
import { ListingDetails } from "./ListingDetails";
import { ProductPresentationEditor } from "./ProductPresentationEditor";

function stubLocalStorage() {
  const bag = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => bag.get(k) ?? null,
    setItem: (k: string, v: string) => bag.set(k, v),
    removeItem: (k: string) => bag.delete(k),
    clear: () => bag.clear(),
    key: () => null,
    length: 0,
  });
}

const DEMO_SVC_ITEM_ID = "a1000003-0003-4000-8000-000000000003";

function svcItem(): PartnerCatalogItem {
  return {
    id: DEMO_SVC_ITEM_ID,
    partnerId: "p-svc",
    code: "SVC-BASIC",
    name: "Basic tier",
    description: "Single channel",
    merchantBenefit: "تواصل مباشر مع عملائك.",
    offeringKind: "ServiceOneOff",
    participationMode: "SubscriptionFee",
    partnerCost: { amount: 49, currency: "SAR", vatInclusive: true },
    settlementBook: "Integration",
    status: "Active",
  };
}

const NO_PACKAGES: UsagePackage[] = [];

describe("Gate 2a — listing authoring + merchant rendering", () => {
  beforeEach(() => {
    stubLocalStorage();
    resetListings();
  });

  it("service partner (canEdit) gets the five editable sections with cap counters", () => {
    const mode = resolveCatalogAuthoringMode({ partnerType: "Service", canAuthorSelf: true, canAuthorManaged: false });
    expect(canAuthorOfferingPresentation(mode)).toBe(true);

    const html = renderToStaticMarkup(
      <ProductPresentationEditor
        lang="ar"
        item={svcItem()}
        packages={NO_PACKAGES}
        canEdit
        onBack={() => {}}
        onSaveOffering={() => {}}
        onSavePackageNote={() => {}}
        listing={getListing(DEMO_SVC_ITEM_ID)}
        onSaveListing={() => {}}
      />,
    );

    expect(html).toContain('data-testid="listing-editor"');
    for (const section of ["requirements", "deliverables", "steps", "terms", "faqs"]) {
      expect(html).toContain(`data-testid="listing-section-${section}"`);
    }
    expect(html).toContain('data-testid="listing-save"');
    expect(html).toContain("3/10"); // requirements cap counter on the seeded listing
    expect(html).toContain('dir="rtl"');
  });

  it("delivery/3PL (read-only) renders the listing as merchant details — no editor, no save", () => {
    const mode = resolveCatalogAuthoringMode({ partnerType: "ThreePL", canAuthorSelf: true, canAuthorManaged: false });
    expect(canAuthorOfferingPresentation(mode)).toBe(false);

    const html = renderToStaticMarkup(
      <ProductPresentationEditor
        lang="ar"
        item={svcItem()}
        packages={NO_PACKAGES}
        canEdit={false}
        onBack={() => {}}
        onSaveOffering={() => {}}
        onSavePackageNote={() => {}}
        listing={getListing(DEMO_SVC_ITEM_ID)}
        onSaveListing={() => {}}
      />,
    );

    expect(html).toContain('data-testid="listing-details"');
    expect(html).not.toContain('data-testid="listing-editor"');
    expect(html).not.toContain('data-testid="listing-save"');
  });

  it("merchant details render only non-empty sections; an empty listing renders nothing", () => {
    const partial: OfferingListing = {
      ...emptyListing("item-x"),
      deliverables: [{ id: "d1", orderIndex: 0, title: "تقرير أداء", quantity: 2 }],
      faqs: [{ id: "f1", orderIndex: 0, question: "متى؟", answer: "خلال خمسة أيام." }],
    };

    const html = renderToStaticMarkup(<ListingDetails lang="ar" listing={partial} />);
    expect(html).toContain('data-testid="listing-details-deliverables"');
    expect(html).toContain('data-testid="listing-details-faqs"');
    expect(html).not.toContain('data-testid="listing-details-requirements"');
    expect(html).not.toContain('data-testid="listing-details-steps"');
    expect(html).not.toContain('data-testid="listing-details-terms"');
    expect(html).toContain("× 2"); // deliverable quantity
    expect(html).toContain("<details"); // FAQ accordion

    expect(renderToStaticMarkup(<ListingDetails lang="ar" listing={emptyListing("item-y")} />)).toBe("");
  });

  it("STRUCTURAL SCOPE (6b standard): the populated merchant listing graph carries no buy/margin/buyRate keys", () => {
    const seeded = getListing(DEMO_SVC_ITEM_ID); // POPULATED fixture — a leak would surface
    expect(seeded.requirements.length).toBeGreaterThan(0);

    const forbidden = ["buy", "buyRate", "margin", "partnerCost", "baseBuyAmount", "overageBuyAmount"];

    const walk = (value: unknown, path = "listing"): void => {
      if (value == null || typeof value !== "object") return;
      for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
        expect(forbidden.includes(key), `forbidden key "${key}" at ${path}.${key}`).toBe(false);
        walk(child, `${path}.${key}`);
      }
    };
    walk(seeded);

    // And the rendered merchant markup carries no cost/margin words either.
    const html = renderToStaticMarkup(<ListingDetails lang="en" listing={seeded} />).toLowerCase();
    expect(html).not.toContain("margin");
    expect(html).not.toContain("buy");
  });
});
