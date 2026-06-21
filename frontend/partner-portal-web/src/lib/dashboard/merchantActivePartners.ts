import { projectCatalogPrice } from "@/lib/catalog/catalogPriceVisibility";
import { deriveInvoicePayment, type MerchantActivation, type PaymentStatus, type PortalData } from "@/lib/data/types";

const r2 = (n: number) => Math.round(n * 100) / 100;

/** Merchant invoice/payment read side for one activation. */
export interface MerchantActivationPaymentSide {
  paidToDate: number;
  remaining: number;
  status: PaymentStatus | "None";
}

/** One row on the merchant "My Active Partners" dashboard — merchant sell side ONLY. */
export interface MerchantActivePartnerRow {
  activationId: string;
  partnerId: string;
  partnerName: string;
  tierName: string;
  tierCode: string;
  status: MerchantActivation["status"];
  activeSince?: string;
  sellPrice: { amount: number; currency: string; vatInclusive: boolean };
  payment: MerchantActivationPaymentSide;
}

/** Keys that must be absent from merchant dashboard rows (not CSS-hidden). */
export const MERCHANT_ACTIVE_PARTNER_FORBIDDEN_KEYS = [
  "buyPrice",
  "partnerCost",
  "payable",
  "margin",
  "partnerPayable",
  "owed",
  "disbursed",
] as const;

/**
 * Aggregate payment state for an activation from billing periods (invoice read model).
 * Filter only — uses deriveInvoicePayment per period, no recomputation of fee/VAT.
 */
export function sumActivationInvoicePayment(
  data: PortalData,
  activation: MerchantActivation,
  now: Date = new Date(),
): MerchantActivationPaymentSide {
  const periods = data.billingPeriods.filter(
    (p) => p.partnerId === activation.partnerId && p.tenantId === activation.tenantId,
  );

  if (periods.length === 0) {
    return { paidToDate: 0, remaining: 0, status: "None" };
  }

  let paidToDate = 0;
  let remaining = 0;
  let status: PaymentStatus | "None" = "None";

  for (const period of periods) {
    const state = deriveInvoicePayment(period, now);
    paidToDate = r2(paidToDate + state.amountPaid);
    remaining = r2(remaining + state.amountOutstanding);
    if (state.status === "Overdue") {
      status = "Overdue";
    } else if (status !== "Overdue" && state.status !== "Paid") {
      status = state.status;
    } else if (status === "None" && state.status === "Paid") {
      status = "Paid";
    }
  }

  if (status === "None" && remaining === 0 && paidToDate > 0) {
    status = "Paid";
  }

  return { paidToDate, remaining, status };
}

/**
 * Scoped merchant dashboard — own activations + sell price + invoice payment state.
 */
export function buildMerchantActivePartners(
  data: PortalData,
  tenantId: string,
  partnerNameById: Map<string, string>,
  now: Date = new Date(),
): MerchantActivePartnerRow[] {
  return data.activations
    .filter((a) => a.tenantId === tenantId && a.status !== "Ended")
    .sort((a, b) => {
      const nameA = partnerNameById.get(a.partnerId) ?? a.partnerId;
      const nameB = partnerNameById.get(b.partnerId) ?? b.partnerId;
      return nameA.localeCompare(nameB);
    })
    .map((a) => {
      const scopedSell = projectCatalogPrice({ sell: a.resalePrice }, "merchant");
      const sellPrice = scopedSell.sell ?? a.resalePrice;

      return {
        activationId: a.id,
        partnerId: a.partnerId,
        partnerName: partnerNameById.get(a.partnerId) ?? a.catalogItemName,
        tierName: a.catalogItemName,
        tierCode: data.catalogItems.find((c) => c.id === a.catalogItemId)?.code ?? "",
        status: a.status,
        activeSince: a.activatedAt,
        sellPrice: {
          amount: sellPrice.amount,
          currency: sellPrice.currency,
          vatInclusive: sellPrice.vatInclusive,
        },
        payment: sumActivationInvoicePayment(data, a, now),
      };
    });
}

/** Assert merchant row shape excludes partner buy / margin fields. */
export function assertMerchantRowFieldScope(row: MerchantActivePartnerRow): void {
  const raw = row as unknown as Record<string, unknown>;
  for (const key of MERCHANT_ACTIVE_PARTNER_FORBIDDEN_KEYS) {
    if (key in raw) {
      throw new Error(`Forbidden field "${key}" present on merchant active partner row`);
    }
  }
}
