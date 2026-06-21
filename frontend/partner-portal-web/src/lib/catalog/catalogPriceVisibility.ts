import type { Money } from "@/lib/data/types";
import type { PortalRole } from "@/lib/rbac/portalRoles";

/** Viewer scope for catalog/activation price legs — single source, filter only. */
export type CatalogPriceViewer =
  | "accountant"
  | "admin"
  | "partner"
  | "merchant";

export interface CatalogPriceSource {
  buy?: Money;
  sell?: Money;
}

export interface ScopedCatalogPrice {
  buy?: Money;
  sell?: Money;
  margin?: Money;
}

function marginMoney(buy: Money, sell: Money): Money {
  return {
    amount: Math.round((sell.amount - buy.amount) * 100) / 100,
    currency: sell.currency,
    vatInclusive: sell.vatInclusive,
  };
}

/**
 * Project buy/sell/margin for a viewer. Fields are omitted (absent), never zero-filled
 * placeholders — callers must not render keys that are undefined.
 */
export function projectCatalogPrice(
  source: CatalogPriceSource,
  viewer: CatalogPriceViewer,
): ScopedCatalogPrice {
  const buy = source.buy;
  const sell = source.sell;
  const canSeeBuy = viewer === "accountant" || viewer === "admin" || viewer === "partner";
  const canSeeSell = viewer === "accountant" || viewer === "admin" || viewer === "merchant";
  const canSeeMargin =
    (viewer === "accountant" || viewer === "admin") && buy != null && sell != null;

  const result: ScopedCatalogPrice = {};
  if (canSeeBuy && buy) {
    result.buy = buy;
  }
  if (canSeeSell && sell) {
    result.sell = sell;
  }
  if (canSeeMargin && buy && sell) {
    result.margin = marginMoney(buy, sell);
  }
  return result;
}

export function portalRoleToPriceViewer(role: PortalRole): CatalogPriceViewer {
  switch (role) {
    case "Accountant":
      return "accountant";
    case "PlatformAdmin":
    case "PartnerSuccessManager":
      return "admin";
    case "PartnerFinance":
      return "partner";
    case "MerchantPreview":
      return "merchant";
    default:
      return "admin";
  }
}

/** Keys that must be absent for a given viewer (for test assertions). */
export function forbiddenPriceKeys(viewer: CatalogPriceViewer): Array<keyof ScopedCatalogPrice> {
  switch (viewer) {
    case "partner":
      return ["sell", "margin"];
    case "merchant":
      return ["buy", "margin"];
    default:
      return [];
  }
}
