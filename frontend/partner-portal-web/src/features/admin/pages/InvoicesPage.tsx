import { BillingPage } from "./BillingPage";
import type { ModuleScopeProps } from "../moduleScope";

/** Subscriptions module — invoice list (reuses billing screen, invoices focus). */
export function InvoicesPage(props: ModuleScopeProps) {
  return <BillingPage {...props} titleKey="moduleScreen_invoices" descKey="invoicesDesc" invoicesOnly />;
}
