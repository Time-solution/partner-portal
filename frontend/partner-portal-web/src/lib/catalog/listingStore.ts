import {
  emptyListing,
  normalizeListing,
  validateListing,
  type OfferingListing,
} from "./listingSchema";

/**
 * Mock store for structured listings (localStorage sibling — the bankRegistryStore pattern),
 * keyed by catalog item id. Whole-document replace; empty sections clear; rows are re-indexed
 * contiguous on save (app-managed ordering). Presentation only — no order lifecycle.
 */

const STORAGE_KEY = "zahy-catalog-listings-v1";

/** The seeded ACTIVE service offering (WhatsApp Basic tier) used across the demo. */
const DEMO_SVC_ITEM_ID = "a1000003-0003-4000-8000-000000000003";

function seedDefaults(): Record<string, OfferingListing> {
  const demo: OfferingListing = normalizeListing({
    catalogItemId: DEMO_SVC_ITEM_ID,
    requirements: [
      { id: "req-1", orderIndex: 0, title: "شعار المتجر بجودة عالية", type: "FileUpload", choices: [] },
      { id: "req-2", orderIndex: 0, title: "نبذة عن نشاط المتجر", type: "ShortText", choices: [] },
      {
        id: "req-3",
        orderIndex: 0,
        title: "الفئة المستهدفة",
        type: "MultiChoice",
        choices: ["أفراد", "شركات", "الاثنان معًا"],
      },
    ],
    deliverables: [
      { id: "del-1", orderIndex: 0, title: "قالب رسائل واتساب معتمد", quantity: 3 },
      { id: "del-2", orderIndex: 0, title: "تقرير أداء شهري", quantity: 1 },
    ],
    executionSteps: [
      { id: "step-1", orderIndex: 0, text: "استلام المتطلبات ومراجعتها" },
      { id: "step-2", orderIndex: 0, text: "إعداد القوالب وتفعيل القناة" },
      { id: "step-3", orderIndex: 0, text: "التسليم والتدريب على الاستخدام" },
    ],
    terms: [
      { id: "term-1", orderIndex: 0, text: "جولتا مراجعة مشمولتان ضمن الخدمة" },
      { id: "term-2", orderIndex: 0, text: "التسليم خلال خمسة أيام عمل من اكتمال المتطلبات" },
    ],
    faqs: [
      {
        id: "faq-1",
        orderIndex: 0,
        question: "متى يبدأ العمل على الخدمة؟",
        answer: "بعد اكتمال جميع المتطلبات المذكورة أعلاه مباشرة.",
      },
    ],
  });

  return { [DEMO_SVC_ITEM_ID]: demo };
}

function load(): Record<string, OfferingListing> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return seedDefaults();
    const parsed = JSON.parse(raw) as Record<string, OfferingListing>;
    return parsed && typeof parsed === "object" ? parsed : seedDefaults();
  } catch {
    return seedDefaults();
  }
}

function persist(map: Record<string, OfferingListing>): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(map));
  } catch {
    // mock store — persistence is best-effort
  }
}

export function getListing(catalogItemId: string): OfferingListing {
  return load()[catalogItemId] ?? emptyListing(catalogItemId);
}

/** Validates, normalizes (contiguous orderIndex), and saves. Returns null on success or the issues. */
export function saveListing(listing: OfferingListing): ReturnType<typeof validateListing> | null {
  const issues = validateListing(listing);
  if (issues.length > 0) return issues;

  const map = load();
  map[listing.catalogItemId] = normalizeListing(listing);
  persist(map);
  return null;
}

export function resetListings(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // mock store
  }
}
