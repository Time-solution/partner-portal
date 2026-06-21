import type { ReflectedPartnerOrder } from "@/lib/data/types";
import type { Lang } from "@/lib/i18n";
import { formatOrderRef } from "@/lib/format/recordId";
import { formatMoneyNumber } from "@/lib/format/moneyDisplay";

export type ReflectionViewer = "full" | "merchant";

/** i18n keys that must never appear in the merchant reflection detail panel. */
export const PARTNER_ONLY_I18N_KEYS = [
  "reflListPrice",
  "reflDeliveryFee",
  "reflCustomerPaid",
  "reflZahyFee",
] as const;

export type ReflectionDetailRow = {
  labelKey: string;
  label: string;
  value: string;
  moneyAmount?: number;
};

export function filterMerchantReflectionOrders(
  orders: ReflectedPartnerOrder[],
  tenantId: string,
): ReflectedPartnerOrder[] {
  return orders.filter((o) => o.tenantId === tenantId && o.reflection != null);
}

export function reflectionDetailLabelKeys(
  viewer: ReflectionViewer,
  hasZahyFee = false,
): string[] {
  if (viewer === "merchant") {
    return [
      "merchantReflectionDate",
      "merchantReflectionItem",
      "merchantReflectionPrice",
      "merchantReflectionViaPartner",
      "merchantReflectionServiceType",
    ];
  }
  const keys = ["reflMenuPrice", "reflListPrice", "reflDeliveryFee", "reflCustomerPaid"];
  if (hasZahyFee) keys.push("reflZahyFee");
  return keys;
}

export function merchantReflectionItemLabel(order: ReflectedPartnerOrder, lang: Lang): string {
  return order.orderLineId || formatOrderRef(order.externalTransactionId, lang);
}

export function buildReflectionDetailRows(
  order: ReflectedPartnerOrder,
  viewer: ReflectionViewer,
  lang: Lang,
  t: (key: string) => string,
): ReflectionDetailRow[] {
  const d = order.reflection!;

  if (viewer === "merchant") {
    return [
      {
        labelKey: "merchantReflectionDate",
        label: t("merchantReflectionDate"),
        value: new Date(order.reflectedAt).toLocaleString(lang === "ar" ? "ar-SA" : "en-GB"),
      },
      {
        labelKey: "merchantReflectionItem",
        label: t("merchantReflectionItem"),
        value: merchantReflectionItemLabel(order, lang),
      },
      {
        labelKey: "merchantReflectionPrice",
        label: t("merchantReflectionPrice"),
        value: formatMoneyNumber(d.merchantMenuPrice.amount),
        moneyAmount: d.merchantMenuPrice.amount,
      },
      {
        labelKey: "merchantReflectionViaPartner",
        label: t("merchantReflectionViaPartner").replace("{partner}", order.partnerName),
        value: "",
      },
      {
        labelKey: "merchantReflectionServiceType",
        label: t("merchantReflectionServiceType"),
        value: t("merchantReflectionServiceReflection"),
      },
    ];
  }

  const rows: ReflectionDetailRow[] = [
    {
      labelKey: "reflMenuPrice",
      label: t("reflMenuPrice"),
      value: formatMoneyNumber(d.merchantMenuPrice.amount),
      moneyAmount: d.merchantMenuPrice.amount,
    },
    {
      labelKey: "reflListPrice",
      label: t("reflListPrice"),
      value: formatMoneyNumber(d.partnerListPrice.amount),
      moneyAmount: d.partnerListPrice.amount,
    },
    {
      labelKey: "reflDeliveryFee",
      label: t("reflDeliveryFee"),
      value: formatMoneyNumber(d.deliveryFee.amount),
      moneyAmount: d.deliveryFee.amount,
    },
    {
      labelKey: "reflCustomerPaid",
      label: t("reflCustomerPaid"),
      value: formatMoneyNumber(d.customerPaid.amount),
      moneyAmount: d.customerPaid.amount,
    },
  ];

  if (d.zahyFeeInclusive) {
    rows.push({
      labelKey: "reflZahyFee",
      label: t("reflZahyFee"),
      value: formatMoneyNumber(d.zahyFeeInclusive.amount),
      moneyAmount: d.zahyFeeInclusive.amount,
    });
  }

  return rows;
}

/** Flat text of visible detail rows — used by unit tests for viewer scoping. */
export function buildReflectionDetailText(
  order: ReflectedPartnerOrder,
  viewer: ReflectionViewer,
  lang: Lang,
  t: (key: string) => string,
): string {
  const rows = buildReflectionDetailRows(order, viewer, lang, t);
  const lines = rows.map((r) => (r.value ? `${r.label}: ${r.value}` : r.label));
  return lines.join("\n");
}
