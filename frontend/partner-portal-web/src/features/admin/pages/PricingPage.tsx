import { CatalogPage } from "./CatalogPage";
import type { ModuleScopeProps } from "../moduleScope";

/** Delivery module — buy/sell pricing view (reuses catalog screen). */
export function PricingPage(props: ModuleScopeProps) {
  return <CatalogPage {...props} titleKey="moduleScreen_pricing" descKey="pricingDesc" />;
}
