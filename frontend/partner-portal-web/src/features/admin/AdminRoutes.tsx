import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import {
  canAccessFinanceWorkspace,
  canAccessMerchantPreview,
} from "@/lib/rbac/partnerModules";
import {
  defaultLandingPath,
  isPathAllowedForRole,
  isPartnerScopedRole,
  mockScopedPartnerId,
} from "@/lib/rbac/roleNavConfig";
import { usePortalSession } from "@/features/auth/usePortalSession";
import type { PortalRole } from "@/lib/rbac/portalRoles";
import type { Lang } from "@/lib/i18n";
import { DashboardPage } from "./pages/DashboardPage";
import { PartnersListPage } from "./pages/PartnersListPage";
import { PartnerDetailPage } from "./pages/PartnerDetailPage";
import { PartnerModulePage } from "./pages/PartnerModulePage";
import { FinanceWorkspacePage } from "./pages/FinanceWorkspacePage";
import { SettingsPage } from "./pages/SettingsPage";
import { WebhooksPage } from "./pages/WebhooksPage";
import { CredentialsPage } from "./pages/CredentialsPage";
import { MerchantPreviewPage } from "./pages/MerchantPreviewPage";

function RoleFallback() {
  const { role } = usePortalSession();
  return <Navigate to={defaultLandingPath(role as PortalRole)} replace />;
}

function RequirePermission({
  permissions,
  children,
}: {
  permissions: readonly string[];
  children: React.ReactNode;
}) {
  const { canAny } = usePortalSession();
  if (!canAny(permissions)) {
    return <RoleFallback />;
  }
  return <>{children}</>;
}

function RequireFinance({ children }: { children: React.ReactNode }) {
  const { role } = usePortalSession();
  if (!canAccessFinanceWorkspace(role as PortalRole)) {
    return <RoleFallback />;
  }
  return <>{children}</>;
}

function RequireMerchantPreview({ children }: { children: React.ReactNode }) {
  const { role } = usePortalSession();
  if (!canAccessMerchantPreview(role as PortalRole)) {
    return <RoleFallback />;
  }
  return <>{children}</>;
}

function RequireAdminExperience({ children }: { children: React.ReactNode }) {
  const { role } = usePortalSession();
  if (isPartnerScopedRole(role) || role === "MerchantPreview") {
    return <RoleFallback />;
  }
  return <>{children}</>;
}

function ScopedRouteGuard({ children }: { children: React.ReactNode }) {
  const { role } = usePortalSession();
  const location = useLocation();
  const portalRole = role as PortalRole;

  if (!isPathAllowedForRole(location.pathname, portalRole)) {
    return <Navigate to={defaultLandingPath(portalRole)} replace />;
  }

  return <>{children}</>;
}

function HomeRedirect() {
  const { role } = usePortalSession();
  return <Navigate to={defaultLandingPath(role as PortalRole)} replace />;
}

function PartnerAliasRedirect() {
  const { role } = usePortalSession();
  const partnerId = mockScopedPartnerId(role);
  if (!partnerId) return <RoleFallback />;
  return <Navigate to={`/partners/${partnerId}`} replace />;
}

export function AdminRoutes({ lang }: { lang: Lang }) {
  return (
    <ScopedRouteGuard>
      <Routes>
        <Route path="/" element={<HomeRedirect />} />

        <Route path="/partner" element={<PartnerAliasRedirect />} />

        <Route
          path="/dashboard"
          element={
            <RequireAdminExperience>
              <RequirePermission permissions={[PortalPermissions.Dashboard.Read]}>
                <DashboardPage lang={lang} />
              </RequirePermission>
            </RequireAdminExperience>
          }
        />

        <Route
          path="/merchant-preview"
          element={
            <RequireMerchantPreview>
              <MerchantPreviewPage lang={lang} />
            </RequireMerchantPreview>
          }
        />

        <Route
          path="/finance/*"
          element={
            <RequireFinance>
              <FinanceWorkspacePage lang={lang} />
            </RequireFinance>
          }
        />

        <Route
          path="/modules/:moduleId/*"
          element={
            <RequireAdminExperience>
              <RequirePermission
                permissions={[
                  PortalPermissions.Catalog.Read,
                  PortalPermissions.Reflection.Read,
                  PortalPermissions.Billing.Read,
                  PortalPermissions.Settlement.Read,
                  PortalPermissions.Activations.Read,
                ]}
              >
                <PartnerModulePage lang={lang} />
              </RequirePermission>
            </RequireAdminExperience>
          }
        />

        <Route
          path="/partners"
          element={
            <RequireAdminExperience>
              <RequirePermission
                permissions={[PortalPermissions.Partners.Read, PortalPermissions.Partners.Manage]}
              >
                <PartnersListPage lang={lang} />
              </RequirePermission>
            </RequireAdminExperience>
          }
        />
        <Route
          path="/partners/:id"
          element={
            <RequirePermission
              permissions={[
                PortalPermissions.Partners.Read,
                PortalPermissions.Partners.Manage,
                PortalPermissions.PartnerFinance.ReadOwn,
              ]}
            >
              <PartnerDetailPage lang={lang} />
            </RequirePermission>
          }
        />
        <Route
          path="/partners/:id/:tab"
          element={
            <RequirePermission
              permissions={[
                PortalPermissions.Partners.Read,
                PortalPermissions.Partners.Manage,
                PortalPermissions.PartnerFinance.ReadOwn,
              ]}
            >
              <PartnerDetailPage lang={lang} />
            </RequirePermission>
          }
        />

        <Route path="/users" element={<Navigate to="/settings/users" replace />} />

        <Route path="/catalog" element={<Navigate to="/modules/delivery-service/catalog" replace />} />
        <Route path="/activations" element={<Navigate to="/modules/marketplace/activations" replace />} />
        <Route path="/settlement" element={<Navigate to="/finance/settlements" replace />} />
        <Route path="/reflected-orders" element={<Navigate to="/modules/commerce/reflected" replace />} />
        <Route path="/billing" element={<Navigate to="/finance/billing" replace />} />
        <Route path="/reversals" element={<Navigate to="/finance/reversals" replace />} />

        <Route
          path="/settings/*"
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
        <Route path="*" element={<HomeRedirect />} />
      </Routes>
    </ScopedRouteGuard>
  );
}
