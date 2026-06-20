import { BillingPage } from "./BillingPage";
import type { ModuleScopeProps } from "../moduleScope";

/** Subscriptions module — subscription plans overview (reuses billing data). */
export function SubscriptionsOverviewPage(props: ModuleScopeProps) {
  return <BillingPage {...props} titleKey="moduleScreen_subscriptions" descKey="subscriptionsOverviewDesc" />;
}
