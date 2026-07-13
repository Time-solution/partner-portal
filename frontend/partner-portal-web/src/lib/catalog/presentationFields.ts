/**
 * Phase 6b — shared caps + text utilities for the partner-authored PRESENTATION fields
 * (PartnerBrief, MerchantBenefit, OfferingSummary, PackageExplanation). Plain text only,
 * trimmed, length-capped. Values mirror the backend PartnerCatalogConsts so the mock UI and
 * the API agree. Presentation only — never pricing.
 */
export const MAX_PARTNER_BRIEF = 600;
export const MAX_MERCHANT_BENEFIT = 400;
export const MAX_OFFERING_SUMMARY = 400;
export const MAX_PACKAGE_EXPLANATION = 500;

/** Trim and hard-cap to the field length (defensive — the input also enforces maxLength). */
export function clampPresentation(text: string, max: number): string {
  return text.trim().slice(0, max);
}

/** Within cap after trimming. Empty is always valid (every presentation field is optional). */
export function isWithinCap(text: string, max: number): boolean {
  return text.trim().length <= max;
}

/** Normalize an optional presentation field: trimmed string, or undefined when blank. */
export function normalizeOptionalPresentation(text: string | undefined, max: number): string | undefined {
  if (text == null) return undefined;
  const clamped = clampPresentation(text, max);
  return clamped.length > 0 ? clamped : undefined;
}
