/**
 * Gate 2a — structured listing schema (FE mirror of PartnerCatalogListing). Types, caps, the
 * anti-disintermediation validator (ONE FE source — mirrors PartnerCatalogContentPolicy), row
 * validation, and app-managed ordering helpers (contiguous orderIndex rewritten on write — never
 * a DB identity concern in the mock, but the same reorder-on-write contract as the backend).
 * Presentation/config only — no ordering lifecycle, no billing, no posting (Gate 2b builds those).
 */

export type ListingRequirementType = "ShortText" | "LongText" | "FileUpload" | "Link" | "MultiChoice";

export interface ListingRequirementRow {
  id: string;
  orderIndex: number;
  title: string;
  type: ListingRequirementType;
  /** MultiChoice only (≤8, each ≤80). */
  choices: string[];
}

export interface ListingDeliverableRow {
  id: string;
  orderIndex: number;
  title: string;
  quantity: number;
}

export interface ListingTextRow {
  id: string;
  orderIndex: number;
  text: string;
}

export interface ListingFaqRow {
  id: string;
  orderIndex: number;
  question: string;
  answer: string;
}

export interface OfferingListing {
  catalogItemId: string;
  requirements: ListingRequirementRow[];
  deliverables: ListingDeliverableRow[];
  executionSteps: ListingTextRow[];
  terms: ListingTextRow[];
  faqs: ListingFaqRow[];
}

export const LISTING_CAPS = {
  requirements: 10,
  deliverables: 5,
  executionSteps: 10,
  terms: 10,
  faqs: 10,
  choicesPerRequirement: 8,
  requirementTitle: 150,
  choice: 80,
  deliverableTitle: 150,
  executionStep: 300,
  term: 300,
  faqQuestion: 200,
  faqAnswer: 500,
  deliverableQtyMin: 1,
  deliverableQtyMax: 99,
} as const;

/**
 * ANTI-DISINTERMEDIATION default (ratifiable): merchant-facing text carries no URL, email, or
 * phone number. Single FE source — mirrors the backend PartnerCatalogContentPolicy patterns.
 */
const URL_PATTERN =
  /(https?:\/\/|www\.)\S+|(?<![\w@.])[a-zA-Z0-9-]{2,}\.(com|net|org|io|sa|co|me|app|shop|store|info|biz)(?![\w])/i;
const EMAIL_PATTERN = /[\w.+-]+@[\w-]+\.[\w.]{2,}/i;
const PHONE_PATTERN = /(\+?\d[\d\s\-().]{7,}\d)/;

export function containsContactChannel(text: string | undefined | null): boolean {
  if (!text || !text.trim()) return false;
  return URL_PATTERN.test(text) || EMAIL_PATTERN.test(text) || PHONE_PATTERN.test(text);
}

export function emptyListing(catalogItemId: string): OfferingListing {
  return { catalogItemId, requirements: [], deliverables: [], executionSteps: [], terms: [], faqs: [] };
}

export function isListingEmpty(listing: OfferingListing | undefined | null): boolean {
  if (!listing) return true;
  return (
    listing.requirements.length === 0 &&
    listing.deliverables.length === 0 &&
    listing.executionSteps.length === 0 &&
    listing.terms.length === 0 &&
    listing.faqs.length === 0
  );
}

/** Rewrites every section with contiguous 0-based orderIndex in array order (app-managed ordering). */
export function normalizeListing(listing: OfferingListing): OfferingListing {
  const reindex = <T extends { orderIndex: number }>(rows: T[]): T[] =>
    rows.map((row, index) => ({ ...row, orderIndex: index }));
  return {
    catalogItemId: listing.catalogItemId,
    requirements: reindex(listing.requirements),
    deliverables: reindex(listing.deliverables),
    executionSteps: reindex(listing.executionSteps),
    terms: reindex(listing.terms),
    faqs: reindex(listing.faqs),
  };
}

/** Move a row one position and return a re-indexed array (reorder-on-write proof). */
export function moveRow<T extends { orderIndex: number }>(rows: T[], from: number, to: number): T[] {
  if (to < 0 || to >= rows.length || from === to) return rows;
  const next = [...rows];
  const [row] = next.splice(from, 1);
  next.splice(to, 0, row);
  return next.map((r, index) => ({ ...r, orderIndex: index }));
}

/** i18n keys, not prose — the UI translates. */
export type ListingValidationError =
  | "listingErrRequired"
  | "listingErrTooLong"
  | "listingErrRowCap"
  | "listingErrChoicesOnlyMultiChoice"
  | "listingErrChoicesInvalid"
  | "listingErrQuantityRange"
  | "listingErrContactInfo";

export interface ListingIssue {
  section: keyof Omit<OfferingListing, "catalogItemId">;
  rowIndex: number | null;
  error: ListingValidationError;
}

export function validateListing(listing: OfferingListing): ListingIssue[] {
  const issues: ListingIssue[] = [];
  const push = (section: ListingIssue["section"], rowIndex: number | null, error: ListingValidationError) =>
    issues.push({ section, rowIndex, error });

  const checkText = (
    section: ListingIssue["section"],
    rowIndex: number,
    value: string,
    cap: number,
  ): void => {
    const trimmed = value.trim();
    if (!trimmed) push(section, rowIndex, "listingErrRequired");
    else if (trimmed.length > cap) push(section, rowIndex, "listingErrTooLong");
    if (containsContactChannel(trimmed)) push(section, rowIndex, "listingErrContactInfo");
  };

  if (listing.requirements.length > LISTING_CAPS.requirements) push("requirements", null, "listingErrRowCap");
  listing.requirements.forEach((row, i) => {
    checkText("requirements", i, row.title, LISTING_CAPS.requirementTitle);
    const choices = row.choices.map((c) => c.trim()).filter(Boolean);
    if (row.type !== "MultiChoice") {
      if (choices.length > 0) push("requirements", i, "listingErrChoicesOnlyMultiChoice");
    } else if (choices.length === 0 || choices.length > LISTING_CAPS.choicesPerRequirement) {
      push("requirements", i, "listingErrChoicesInvalid");
    }
    for (const choice of choices) {
      if (choice.length > LISTING_CAPS.choice) push("requirements", i, "listingErrTooLong");
      if (containsContactChannel(choice)) push("requirements", i, "listingErrContactInfo");
    }
  });

  if (listing.deliverables.length > LISTING_CAPS.deliverables) push("deliverables", null, "listingErrRowCap");
  listing.deliverables.forEach((row, i) => {
    checkText("deliverables", i, row.title, LISTING_CAPS.deliverableTitle);
    if (
      !Number.isInteger(row.quantity) ||
      row.quantity < LISTING_CAPS.deliverableQtyMin ||
      row.quantity > LISTING_CAPS.deliverableQtyMax
    ) {
      push("deliverables", i, "listingErrQuantityRange");
    }
  });

  if (listing.executionSteps.length > LISTING_CAPS.executionSteps) push("executionSteps", null, "listingErrRowCap");
  listing.executionSteps.forEach((row, i) => checkText("executionSteps", i, row.text, LISTING_CAPS.executionStep));

  if (listing.terms.length > LISTING_CAPS.terms) push("terms", null, "listingErrRowCap");
  listing.terms.forEach((row, i) => checkText("terms", i, row.text, LISTING_CAPS.term));

  if (listing.faqs.length > LISTING_CAPS.faqs) push("faqs", null, "listingErrRowCap");
  listing.faqs.forEach((row, i) => {
    checkText("faqs", i, row.question, LISTING_CAPS.faqQuestion);
    checkText("faqs", i, row.answer, LISTING_CAPS.faqAnswer);
  });

  return issues;
}
