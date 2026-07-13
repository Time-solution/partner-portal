import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  containsContactChannel,
  emptyListing,
  isListingEmpty,
  LISTING_CAPS,
  moveRow,
  normalizeListing,
  validateListing,
  type OfferingListing,
} from "./listingSchema";
import { getListing, resetListings, saveListing } from "./listingStore";

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

const text = (id: string, textValue: string) => ({ id, orderIndex: 0, text: textValue });

function baseListing(): OfferingListing {
  return {
    ...emptyListing("item-1"),
    requirements: [{ id: "r1", orderIndex: 0, title: "Store logo", type: "FileUpload", choices: [] }],
    deliverables: [{ id: "d1", orderIndex: 0, title: "Monthly report", quantity: 1 }],
    executionSteps: [text("s1", "Kickoff")],
    terms: [text("t1", "Two revisions included")],
    faqs: [{ id: "f1", orderIndex: 0, question: "How fast?", answer: "Five days." }],
  };
}

describe("listing schema — caps, MultiChoice, quantity, optionality", () => {
  it("row caps are enforced per section (over-cap rejected, at-cap passes)", () => {
    const over = baseListing();
    over.requirements = Array.from({ length: LISTING_CAPS.requirements + 1 }, (_, i) => ({
      id: `r${i}`,
      orderIndex: i,
      title: `Req ${i}`,
      type: "ShortText" as const,
      choices: [],
    }));
    expect(validateListing(over).some((i) => i.section === "requirements" && i.error === "listingErrRowCap")).toBe(true);

    const atCap = baseListing();
    atCap.deliverables = Array.from({ length: LISTING_CAPS.deliverables }, (_, i) => ({
      id: `d${i}`,
      orderIndex: i,
      title: `Del ${i}`,
      quantity: 1,
    }));
    expect(validateListing(atCap)).toHaveLength(0);
  });

  it("field caps and required fields are enforced", () => {
    const listing = baseListing();
    listing.faqs = [{ id: "f1", orderIndex: 0, question: "Q", answer: "x".repeat(LISTING_CAPS.faqAnswer + 1) }];
    expect(validateListing(listing).some((i) => i.error === "listingErrTooLong")).toBe(true);

    listing.faqs = [{ id: "f1", orderIndex: 0, question: "  ", answer: "A" }];
    expect(validateListing(listing).some((i) => i.error === "listingErrRequired")).toBe(true);
  });

  it("choices are MultiChoice-only and capped at 8; FileUpload is only a named type", () => {
    const listing = baseListing();
    listing.requirements = [{ id: "r1", orderIndex: 0, title: "Pick", type: "ShortText", choices: ["A"] }];
    expect(validateListing(listing).some((i) => i.error === "listingErrChoicesOnlyMultiChoice")).toBe(true);

    listing.requirements = [{ id: "r1", orderIndex: 0, title: "Pick", type: "MultiChoice", choices: [] }];
    expect(validateListing(listing).some((i) => i.error === "listingErrChoicesInvalid")).toBe(true);

    listing.requirements = [
      { id: "r1", orderIndex: 0, title: "Pick", type: "MultiChoice", choices: Array.from({ length: 9 }, (_, i) => `C${i}`) },
    ];
    expect(validateListing(listing).some((i) => i.error === "listingErrChoicesInvalid")).toBe(true);

    listing.requirements = [{ id: "r1", orderIndex: 0, title: "Assets", type: "FileUpload", choices: [] }];
    expect(validateListing(listing)).toHaveLength(0); // no upload machinery — just a type name
  });

  it("a choice longer than 80 chars is rejected as too long; 80 exactly passes", () => {
    const over = baseListing();
    over.requirements = [
      { id: "r1", orderIndex: 0, title: "Pick", type: "MultiChoice", choices: ["x".repeat(LISTING_CAPS.choice + 1)] },
    ];
    expect(validateListing(over).some((i) => i.error === "listingErrTooLong")).toBe(true);

    const atCap = baseListing();
    atCap.requirements = [
      { id: "r1", orderIndex: 0, title: "Pick", type: "MultiChoice", choices: ["x".repeat(LISTING_CAPS.choice)] },
    ];
    expect(validateListing(atCap)).toHaveLength(0);
  });

  it("deliverable quantity must be 1–99", () => {
    for (const quantity of [0, 100, 1.5]) {
      const listing = baseListing();
      listing.deliverables = [{ id: "d1", orderIndex: 0, title: "Posts", quantity }];
      expect(validateListing(listing).some((i) => i.error === "listingErrQuantityRange")).toBe(true);
    }
  });

  it("empty sections are valid and an empty listing is empty", () => {
    expect(validateListing(emptyListing("x"))).toHaveLength(0);
    expect(isListingEmpty(emptyListing("x"))).toBe(true);
    expect(isListingEmpty(baseListing())).toBe(false);
  });
});

describe("listing schema — anti-disintermediation (single FE source)", () => {
  const dirty = [
    "visit https://vendor.com",
    "see www.vendor.sa",
    "mail me a@b.com",
    "call +966 55 123 4567",
    "whatsapp 0551234567",
  ];

  it("URL / email / phone are rejected in every merchant-facing field", () => {
    for (const value of dirty) {
      expect(containsContactChannel(value)).toBe(true);

      const inReq = baseListing();
      inReq.requirements = [{ id: "r1", orderIndex: 0, title: value, type: "ShortText", choices: [] }];
      expect(validateListing(inReq).some((i) => i.error === "listingErrContactInfo")).toBe(true);

      const inChoice = baseListing();
      inChoice.requirements = [{ id: "r1", orderIndex: 0, title: "Pick", type: "MultiChoice", choices: [value] }];
      expect(validateListing(inChoice).some((i) => i.error === "listingErrContactInfo")).toBe(true);

      const inStep = baseListing();
      inStep.executionSteps = [text("s1", value)];
      expect(validateListing(inStep).some((i) => i.error === "listingErrContactInfo")).toBe(true);

      const inTerm = baseListing();
      inTerm.terms = [text("t1", value)];
      expect(validateListing(inTerm).some((i) => i.error === "listingErrContactInfo")).toBe(true);

      const inFaq = baseListing();
      inFaq.faqs = [{ id: "f1", orderIndex: 0, question: "Q", answer: value }];
      expect(validateListing(inFaq).some((i) => i.error === "listingErrContactInfo")).toBe(true);
    }
  });

  it("clean Arabic/English service text passes", () => {
    expect(containsContactChannel("نوفّر خمسة منشورات شهريًا مع تقرير أداء")).toBe(false);
    expect(containsContactChannel("Two revision rounds. Delivery in 5 business days.")).toBe(false);
    expect(validateListing(baseListing())).toHaveLength(0);
  });
});

describe("listing ordering — app-managed, contiguous", () => {
  it("moveRow reorders and rewrites contiguous orderIndex", () => {
    const rows = [text("a", "A"), text("b", "B"), text("c", "C")].map((r, i) => ({ ...r, orderIndex: i }));
    const moved = moveRow(rows, 2, 0);
    expect(moved.map((r) => r.text)).toEqual(["C", "A", "B"]);
    expect(moved.map((r) => r.orderIndex)).toEqual([0, 1, 2]);
  });

  it("normalizeListing rewrites every section contiguous from zero", () => {
    const listing = baseListing();
    listing.executionSteps = [
      { id: "s1", orderIndex: 7, text: "First" },
      { id: "s2", orderIndex: 3, text: "Second" },
    ];
    const normalized = normalizeListing(listing);
    expect(normalized.executionSteps.map((s) => s.orderIndex)).toEqual([0, 1]);
  });
});

describe("listing store — save/load + demo seed", () => {
  beforeEach(() => {
    stubLocalStorage();
    resetListings();
  });

  it("saveListing rejects contact info and persists a normalized listing on success", () => {
    const dirty = baseListing();
    dirty.terms = [text("t1", "call 0551234567")];
    expect(saveListing(dirty)).not.toBeNull(); // issues returned, nothing saved
    expect(isListingEmpty(getListing("item-1"))).toBe(true);

    const clean = baseListing();
    clean.executionSteps = [
      { id: "s1", orderIndex: 9, text: "Later" },
      { id: "s2", orderIndex: 1, text: "Sooner" },
    ];
    expect(saveListing(clean)).toBeNull();
    const stored = getListing("item-1");
    expect(stored.executionSteps.map((s) => s.orderIndex)).toEqual([0, 1]); // contiguous on write
  });

  it("saving with all sections empty clears the listing", () => {
    expect(saveListing(baseListing())).toBeNull();
    expect(isListingEmpty(getListing("item-1"))).toBe(false);

    expect(saveListing(emptyListing("item-1"))).toBeNull();
    expect(isListingEmpty(getListing("item-1"))).toBe(true);
  });

  it("the demo seed carries a populated listing for the seeded service offering", () => {
    const seeded = getListing("a1000003-0003-4000-8000-000000000003");
    expect(isListingEmpty(seeded)).toBe(false);
    expect(seeded.requirements.some((r) => r.type === "MultiChoice")).toBe(true);
    expect(validateListing(seeded)).toHaveLength(0);
  });
});
