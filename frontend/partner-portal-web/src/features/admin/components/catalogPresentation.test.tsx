import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import {
  canAuthorOfferingPresentation,
  resolveCatalogAuthoringMode,
} from "@/lib/catalog/catalogAuthoring";
import {
  clampPresentation,
  isWithinCap,
  normalizeOptionalPresentation,
  MAX_MERCHANT_BENEFIT,
  MAX_PACKAGE_EXPLANATION,
  MAX_PARTNER_BRIEF,
} from "@/lib/catalog/presentationFields";
import {
  assertMerchantPackageScope,
  merchantPackageDisplay,
} from "@/lib/usage/usagePackageDisplay";
import type { UsagePackage } from "@/lib/usage/usagePackage";
import { listUsagePackages, resetUsagePackages, setPackageExplanation } from "@/lib/usage/usagePackageStore";
import type { MerchantBrowsePartnerEntry } from "@/lib/catalog/merchantBrowse";
import type { Partner, PartnerCatalogItem } from "@/lib/data/types";
import { createSeedData } from "@/lib/data/fixtures";
import { MockPortalDataSource } from "@/lib/data/mockDataSource";
import { savePersistedData } from "@/lib/data/mockStore";
import { PartnerBriefEditor } from "./PartnerBriefEditor";
import { ProductPresentationEditor } from "./ProductPresentationEditor";
import { PackageDetailsCard } from "./PackageDetailsCard";
import { PartnerBrowseCard } from "../pages/MerchantPreviewPage";

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

const RESALE_PKG: UsagePackage = {
  id: "pkg-test-resale",
  partnerId: "p-svc",
  name: "Resale Flat 5k",
  unitLabel: "messages",
  mode: "Resale",
  currency: "SAR",
  includedQuantity: 5000,
  baseBuyAmount: 150, // partner buy — must NEVER reach the merchant UI
  baseSellAmount: 250,
  overageBuyAmount: 0.03, // buy — must NEVER reach the merchant UI
  overageSellAmount: 0.05,
  payer: "Merchant",
  status: "Published",
  packageExplanation: "يشمل ٥٬٠٠٠ رسالة شهريًا.",
};

function svcOffering(): PartnerCatalogItem {
  return {
    id: "cat-svc-basic",
    partnerId: "p-svc",
    code: "SVC-BASIC",
    name: "Basic tier",
    description: "Single channel",
    merchantBenefit: "تواصل مباشر مع عملائك عبر واتساب.",
    offeringKind: "ServiceOneOff",
    participationMode: "SubscriptionFee",
    partnerCost: { amount: 49, currency: "SAR", vatInclusive: true },
    settlementBook: "Integration",
    status: "Active",
  };
}

function servicePartner(): Partner {
  return {
    id: "p-svc",
    legalName: "WhatsApp Business Co.",
    tradeName: "WhatsApp Co",
    type: "Service",
    status: "Active",
    primaryContactEmail: "billing@whatsapp.co",
    participationMode: "SubscriptionFee",
    accentClass: "border-l-green-500",
    partnerBrief: "نوفّر منصة رسائل واتساب للأعمال لزيادة تفاعل العملاء.",
  };
}

function browseEntry(): MerchantBrowsePartnerEntry {
  return {
    partner: servicePartner(),
    moduleId: "subscriptions",
    offerings: [svcOffering()],
    hasTierMenu: false,
  };
}

describe("Catalog Phase 6b — authoring-by-type gate", () => {
  it("PartnerBrief is editable for ALL partner types (it is self-description, not pricing)", () => {
    // A delivery/3PL (admin-managed) partner: offering presentation is read-only…
    const adminManaged = resolveCatalogAuthoringMode({
      partnerType: "ThreePL",
      canAuthorSelf: true,
      canAuthorManaged: false,
    });
    expect(canAuthorOfferingPresentation(adminManaged)).toBe(false);

    // …but the brief editor is still rendered editable for that partner.
    const html = renderToStaticMarkup(
      <PartnerBriefEditor lang="ar" brief="نبذة" canEdit onSave={() => {}} />,
    );
    expect(html).toContain('data-testid="partner-brief-input"');
    expect(html).toContain('data-testid="partner-brief-save"');
    expect(html).toContain('dir="rtl"');
  });

  it("service partner self-authors offering presentation; delivery/3PL is read-only", () => {
    const selfService = resolveCatalogAuthoringMode({
      partnerType: "Service",
      canAuthorSelf: true,
      canAuthorManaged: false,
    });
    const adminWrite = resolveCatalogAuthoringMode({
      partnerType: "Carrier",
      canAuthorSelf: false,
      canAuthorManaged: true,
    });
    const adminReadonly = resolveCatalogAuthoringMode({
      partnerType: "Carrier",
      canAuthorSelf: true,
      canAuthorManaged: false,
    });
    expect(canAuthorOfferingPresentation(selfService)).toBe(true);
    expect(canAuthorOfferingPresentation(adminWrite)).toBe(true);
    expect(canAuthorOfferingPresentation(adminReadonly)).toBe(false);

    // Read-only editor: no benefit/summary inputs, shows the admin-managed notice.
    const readOnly = renderToStaticMarkup(
      <ProductPresentationEditor
        lang="ar"
        item={svcOffering()}
        packages={[RESALE_PKG]}
        canEdit={false}
        onBack={() => {}}
        onSaveOffering={() => {}}
        onSavePackageNote={() => {}}
      />,
    );
    expect(readOnly).toContain('data-testid="product-presentation-editor"');
    expect(readOnly).not.toContain('data-testid="benefit-input"');
    expect(readOnly).not.toContain('data-testid="package-note-input"');
  });
});

describe("Catalog Phase 6b — product drill-in editor", () => {
  it("renders the selected product's fields and editable benefit/summary + per-package note when allowed", () => {
    const item = svcOffering();
    const html = renderToStaticMarkup(
      <ProductPresentationEditor
        lang="en"
        item={item}
        packages={[RESALE_PKG]}
        canEdit
        onBack={() => {}}
        onSaveOffering={() => {}}
        onSavePackageNote={() => {}}
      />,
    );
    // The right product is shown.
    expect(html).toContain(item.name);
    expect(html).toContain(item.code);
    // Editable presentation fields seeded from the item.
    expect(html).toContain('data-testid="benefit-input"');
    expect(html).toContain('data-testid="summary-input"');
    expect(html).toContain('data-testid="offering-save"');
    expect(html).toContain(item.merchantBenefit as string);
    // Per-package structured details + editable note.
    expect(html).toContain('data-testid="package-details"');
    expect(html).toContain('data-testid="package-note-input"');
    // Length caps surfaced on the inputs.
    expect(html).toContain(`maxLength="${MAX_MERCHANT_BENEFIT}"`);
    expect(html).toContain(`maxLength="${MAX_PACKAGE_EXPLANATION}"`);
  });
});

describe("Catalog Phase 6b — merchant browse card", () => {
  it("leads with the company brief, then the merchant benefit, then package details + note", () => {
    const html = renderToStaticMarkup(
      <PartnerBrowseCard
        entry={browseEntry()}
        lang="ar"
        expanded={false}
        onToggleExpand={() => {}}
        activeCatalogIds={new Set()}
        busyCatalogId={null}
        onActivate={() => {}}
        packages={[RESALE_PKG]}
      />,
    );
    const brief = servicePartner().partnerBrief as string;
    const benefit = svcOffering().merchantBenefit as string;
    expect(html).toContain('data-testid="browse-partner-brief"');
    expect(html).toContain(brief);
    expect(html).toContain('data-testid="browse-merchant-benefit"');
    expect(html).toContain(benefit);
    expect(html).toContain('data-testid="package-details"');
    expect(html).toContain(RESALE_PKG.packageExplanation as string);
    // Brief must lead the benefit (appear earlier in the markup).
    expect(html.indexOf(brief)).toBeLessThan(html.indexOf(benefit));
  });

  it("REGRESSION: merchant view never renders partner buy price or margin", () => {
    const display = merchantPackageDisplay(RESALE_PKG);
    expect(() => assertMerchantPackageScope(display)).not.toThrow();
    expect(display.baseFee).toBe(250); // sell visible
    expect(display.overageRate).toBe(0.05); // sell visible
    expect(JSON.stringify(display)).not.toContain("150"); // buy absent
    expect(JSON.stringify(display)).not.toContain("0.03"); // buy absent

    const cardHtml = renderToStaticMarkup(<PackageDetailsCard lang="en" pkg={RESALE_PKG} />);
    expect(cardHtml).toContain("0.05"); // overage SELL shown
    expect(cardHtml).not.toContain("150.00"); // base BUY never shown
    expect(cardHtml).not.toContain("0.03"); // overage BUY never shown
    expect(cardHtml.toLowerCase()).not.toContain("margin");

    const browseHtml = renderToStaticMarkup(
      <PartnerBrowseCard
        entry={browseEntry()}
        lang="en"
        expanded={false}
        onToggleExpand={() => {}}
        activeCatalogIds={new Set()}
        busyCatalogId={null}
        onActivate={() => {}}
        packages={[RESALE_PKG]}
      />,
    );
    expect(browseHtml).not.toContain("150.00");
    expect(browseHtml.toLowerCase()).not.toContain("margin");
  });
});

describe("Catalog Phase 6b — caps & optionality", () => {
  it("clamps to the cap, treats empty as optional/undefined, and validates within-cap", () => {
    expect(clampPresentation("  hi  ", MAX_PARTNER_BRIEF)).toBe("hi");
    expect(clampPresentation("x".repeat(700), MAX_PARTNER_BRIEF)).toHaveLength(MAX_PARTNER_BRIEF);
    expect(normalizeOptionalPresentation("   ", MAX_PARTNER_BRIEF)).toBeUndefined();
    expect(normalizeOptionalPresentation(undefined, MAX_PARTNER_BRIEF)).toBeUndefined();
    expect(isWithinCap("x".repeat(MAX_PARTNER_BRIEF), MAX_PARTNER_BRIEF)).toBe(true);
    expect(isWithinCap("x".repeat(MAX_PARTNER_BRIEF + 1), MAX_PARTNER_BRIEF)).toBe(false);
  });

  it("brief editor surfaces the 600 cap on the input", () => {
    const html = renderToStaticMarkup(<PartnerBriefEditor lang="en" canEdit onSave={() => {}} />);
    expect(html).toContain(`maxLength="${MAX_PARTNER_BRIEF}"`);
  });
});

describe("Catalog Phase 6b — authoring persistence (mock)", () => {
  beforeEach(() => {
    stubLocalStorage();
    savePersistedData(createSeedData());
    resetUsagePackages();
  });

  it("updatePartnerBrief sets/clears the partner brief (clamped, all types)", async () => {
    const ds = new MockPortalDataSource();
    const whatsapp = "22222222-2222-2222-2222-222222222004";
    const updated = await ds.updatePartnerBrief(whatsapp, "  new brief  ");
    expect(updated.partnerBrief).toBe("new brief");

    const cleared = await ds.updatePartnerBrief(whatsapp, "   ");
    expect(cleared.partnerBrief).toBeUndefined();
  });

  it("updateCatalogPresentation edits benefit/summary on an ACTIVE item without touching pricing", async () => {
    const ds = new MockPortalDataSource();
    const svcBasic = "a1000003-0003-4000-8000-000000000003";
    const before = (await ds.getCatalogItems()).find((i) => i.id === svcBasic);
    const updated = await ds.updateCatalogPresentation(svcBasic, {
      merchantBenefit: "benefit edited",
      description: "summary edited",
    });
    expect(updated.status).toBe("Active"); // not draft-gated
    expect(updated.merchantBenefit).toBe("benefit edited");
    expect(updated.description).toBe("summary edited");
    expect(updated.partnerCost.amount).toBe(before?.partnerCost.amount); // pricing untouched
  });

  it("setPackageExplanation caps the note and clears on empty", () => {
    const seeded = listUsagePackages().find((p) => p.packageExplanation);
    expect(seeded).toBeTruthy();
    const capped = setPackageExplanation(seeded!.id, "x".repeat(700));
    expect(capped.packageExplanation).toHaveLength(MAX_PACKAGE_EXPLANATION);
    const cleared = setPackageExplanation(seeded!.id, "  ");
    expect(cleared.packageExplanation).toBeUndefined();
  });
});
