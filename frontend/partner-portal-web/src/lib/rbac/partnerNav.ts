import type { ParticipationMode, PartnerType } from "@/lib/data/types";

export interface PartnerSubTab {
  key: string;
  labelEn: string;
  labelAr: string;
  /** Route suffix under /partners/:id/ */
  path: string;
}

export function getPartnerSubTabs(
  partnerType: PartnerType,
  participationMode: ParticipationMode,
): PartnerSubTab[] {
  if (participationMode === "ReflectionOnly") {
    return [
      { key: "reflected", labelEn: "Reflected Orders", labelAr: "الطلبات المعكسة", path: "reflected" },
      { key: "pos-sync", labelEn: "Dashboard / POS sync", labelAr: "مزامنة لوحة/POS", path: "pos-sync" },
    ];
  }

  if (participationMode === "SubscriptionFee") {
    return [
      { key: "subscriptions", labelEn: "Subscriptions", labelAr: "الاشتراكات", path: "subscriptions" },
      { key: "billing-periods", labelEn: "Billing periods", labelAr: "فترات الفوترة", path: "billing-periods" },
      { key: "invoices", labelEn: "Invoices", labelAr: "الفواتير", path: "invoices" },
    ];
  }

  if (participationMode === "Principal") {
    const tabs: PartnerSubTab[] = [
      { key: "catalog", labelEn: "Catalog", labelAr: "الكتالوج", path: "catalog" },
      { key: "pricing", labelEn: "Pricing (buy/sell)", labelAr: "التسعير (شراء/بيع)", path: "pricing" },
      { key: "settlement", labelEn: "Settlement", labelAr: "التسوية", path: "settlement" },
      { key: "reversals", labelEn: "Reversals", labelAr: "العكس", path: "reversals" },
    ];
    if (partnerType === "Marketplace") {
      return [
        { key: "catalog", labelEn: "Catalog", labelAr: "الكتالوج", path: "catalog" },
        { key: "activations", labelEn: "Activations", labelAr: "التفعيلات", path: "activations" },
        { key: "snapshots", labelEn: "Snapshots", labelAr: "لقطات", path: "snapshots" },
        { key: "settlement", labelEn: "Settlement", labelAr: "التسوية", path: "settlement" },
      ];
    }
    return tabs;
  }

  return [{ key: "overview", labelEn: "Overview", labelAr: "نظرة عامة", path: "overview" }];
}

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
