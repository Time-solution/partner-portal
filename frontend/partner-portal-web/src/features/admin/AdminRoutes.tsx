import { Navigate, Route, Routes } from "react-router-dom";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import type { Lang } from "@/lib/i18n";
import { DashboardPage } from "./pages/DashboardPage";
import { PartnersListPage } from "./pages/PartnersListPage";
import { PartnerDetailPage } from "./pages/PartnerDetailPage";
import { CatalogPage } from "./pages/CatalogPage";
import { ActivationsPage } from "./pages/ActivationsPage";
import { SettlementPage } from "./pages/SettlementPage";
import { ReflectedOrdersPage } from "./pages/ReflectedOrdersPage";
import { BillingPage } from "./pages/BillingPage";
import { ReversalsPage } from "./pages/ReversalsPage";
import { SettingsPage } from "./pages/SettingsPage";
import { WebhooksPage } from "./pages/WebhooksPage";
import { CredentialsPage } from "./pages/CredentialsPage";

function RequirePermission({
  permissions,
  children,
}: {
  permissions: readonly string[];
  children: React.ReactNode;
}) {
  const { canAny } = usePortalSession();
  if (!canAny(permissions)) {
    return <Navigate to="/dashboard" replace />;
  }
  return <>{children}</>;
}

export function AdminRoutes({ lang }: { lang: Lang }) {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/dashboard" replace />} />
      <Route
        path="/dashboard"
        element={
          <RequirePermission permissions={[PortalPermissions.Dashboard.Read]}>
            <DashboardPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/partners"
        element={
          <RequirePermission
            permissions={[PortalPermissions.Partners.Read, PortalPermissions.Partners.Manage]}
          >
            <PartnersListPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/partners/:id"
        element={
          <RequirePermission
            permissions={[PortalPermissions.Partners.Read, PortalPermissions.Partners.Manage]}
          >
            <PartnerDetailPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/partners/:id/:tab"
        element={
          <RequirePermission
            permissions={[PortalPermissions.Partners.Read, PortalPermissions.Partners.Manage]}
          >
            <PartnerDetailPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/catalog"
        element={
          <RequirePermission permissions={[PortalPermissions.Catalog.Read]}>
            <CatalogPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/activations"
        element={
          <RequirePermission permissions={[PortalPermissions.Activations.Read]}>
            <ActivationsPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/settlement"
        element={
          <RequirePermission permissions={[PortalPermissions.Settlement.Read]}>
            <SettlementPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/reflected-orders"
        element={
          <RequirePermission permissions={[PortalPermissions.Reflection.Read]}>
            <ReflectedOrdersPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/billing"
        element={
          <RequirePermission permissions={[PortalPermissions.Billing.Read]}>
            <BillingPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/reversals"
        element={
          <RequirePermission permissions={[PortalPermissions.Reversals.Read]}>
            <ReversalsPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/settings"
        element={
          <RequirePermission permissions={[PortalPermissions.Settings.Read]}>
            <SettingsPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/webhooks"
        element={
          <RequirePermission
            permissions={[PortalPermissions.Webhooks.Read, PortalPermissions.Webhooks.Manage]}
          >
            <WebhooksPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route
        path="/credentials"
        element={
          <RequirePermission
            permissions={[PortalPermissions.Credentials.Read, PortalPermissions.Credentials.Rotate]}
          >
            <CredentialsPage lang={lang} />
          </RequirePermission>
        }
      />
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}
