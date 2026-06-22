import type { Lang } from "@/lib/i18n";
import type { PartnerBusinessModuleId, ModuleScreenId } from "@/lib/rbac/partnerModules";
import { ActivationsPage } from "./pages/ActivationsPage";
import { BillingPage } from "./pages/BillingPage";
import { CatalogPage } from "./pages/CatalogPage";
import { InvoicesPage } from "./pages/InvoicesPage";
import { MenuPage } from "./pages/MenuPage";
import { PosSyncPage } from "./pages/PosSyncPage";
import { PricingPage } from "./pages/PricingPage";
import { ReflectedOrdersPage } from "./pages/ReflectedOrdersPage";
import { ReversalsPage } from "./pages/ReversalsPage";
import { SettlementPage } from "./pages/SettlementPage";
import { SnapshotsPage } from "./pages/SnapshotsPage";
import { SubscriptionsOverviewPage } from "./pages/SubscriptionsOverviewPage";
import { UsagePackagesPage } from "./pages/UsagePackagesPage";

export interface ModuleScreenContext {
  lang: Lang;
  moduleId?: PartnerBusinessModuleId;
  /** When set, screen data is scoped to this partner only. */
  partnerId?: string;
  /** Finance workspace — read/reconcile mode, no disburse. */
  financeMode?: boolean;
}

/** Maps screen id → page component. Each screen is built once. */
export function ModuleScreenRenderer({
  screenId,
  context,
}: {
  screenId: ModuleScreenId;
  context: ModuleScreenContext;
}) {
  const { lang, moduleId, partnerId, financeMode } = context;
  const scope = { lang, moduleId, partnerId, financeMode };

  switch (screenId) {
    case "catalog":
      return <CatalogPage {...scope} />;
    case "pricing":
      return <PricingPage {...scope} />;
    case "settlement":
      return <SettlementPage {...scope} />;
    case "reversals":
      return <ReversalsPage {...scope} />;
    case "reflected":
      return <ReflectedOrdersPage {...scope} />;
    case "pos-sync":
      return <PosSyncPage {...scope} />;
    case "menu":
      return <MenuPage {...scope} />;
    case "subscriptions":
      return <SubscriptionsOverviewPage {...scope} />;
    case "usage-packages":
      return <UsagePackagesPage {...scope} />;
    case "billing":
      return <BillingPage {...scope} />;
    case "invoices":
      return <InvoicesPage {...scope} />;
    case "activations":
      return <ActivationsPage {...scope} />;
    case "snapshots":
      return <SnapshotsPage {...scope} />;
    default:
      return null;
  }
}
