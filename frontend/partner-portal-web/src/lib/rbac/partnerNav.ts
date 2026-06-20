import type { ParticipationMode, PartnerType } from "@/lib/data/types";

export function partnerTypeLabel(type: PartnerType, lang: "en" | "ar"): string {
  const map: Record<PartnerType, { en: string; ar: string }> = {
    Marketplace: { en: "Marketplace", ar: "سوق" },
    Service: { en: "Service / Integration", ar: "خدمة / تكامل" },
    Carrier: { en: "Carrier / Delivery", ar: "ناقل / توصيل" },
    Aggregator: { en: "Aggregator (F&B)", ar: "مجمّع (مأكولات)" },
    ThreePL: { en: "3PL", ar: "3PL" },
  };
  return map[type][lang];
}

/** @deprecated Use resolvePartnerModule from partnerModules.ts */
export type ParticipationModeLegacy = ParticipationMode;
