/** Mock merchant tenant for the core-Zahy preview flow — not a portal login in production. */
export const MOCK_MERCHANT_PREVIEW = {
  tenantId: "11111111-1111-1111-1111-111111111099",
  merchantName: "Al Rajhi Demo Store",
} as const;

/** i18n benefit keys keyed by partner id (mock catalogue copy). */
export const MERCHANT_PREVIEW_BENEFIT_KEYS: Record<string, string> = {
  "22222222-2222-2222-2222-222222222001": "merchantPreviewBenefit_salasa",
  "22222222-2222-2222-2222-222222222002": "merchantPreviewBenefit_hungerstation",
  "22222222-2222-2222-2222-222222222003": "merchantPreviewBenefit_noon",
  "22222222-2222-2222-2222-222222222004": "merchantPreviewBenefit_jahez",
  "22222222-2222-2222-2222-222222222005": "merchantPreviewBenefit_supplierHub",
};

export const MERCHANT_PREVIEW_DESC_KEYS: Record<string, string> = {
  "22222222-2222-2222-2222-222222222001": "merchantPreviewDesc_salasa",
  "22222222-2222-2222-2222-222222222002": "merchantPreviewDesc_hungerstation",
  "22222222-2222-2222-2222-222222222003": "merchantPreviewDesc_noon",
  "22222222-2222-2222-2222-222222222004": "merchantPreviewDesc_jahez",
  "22222222-2222-2222-2222-222222222005": "merchantPreviewDesc_supplierHub",
};
