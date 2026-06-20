import { Navigate, Route, Routes } from "react-router-dom";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { canAccessFinanceWorkspace, canAccessMerchantPreview } from "@/lib/rbac/partnerModules";
import { usePortalSession } from "@/features/auth/usePortalSession";
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

function RequireFinance({ children }: { children: React.ReactNode }) {
  const { role } = usePortalSession();
  if (!canAccessFinanceWorkspace(role as never)) {
    return <Navigate to="/dashboard" replace />;
  }
  return <>{children}</>;
}

function RequireMerchantPreview({ children }: { children: React.ReactNode }) {
  const { role } = usePortalSession();
  if (!canAccessMerchantPreview(role as never)) {
    return <Navigate to="/dashboard" replace />;
  }
  return <>{children}</>;
}

function HomeRedirect() {
  const { role } = usePortalSession();
  if (role === "MerchantPreview") return <Navigate to="/merchant-preview" replace />;
  if (role === "Accountant") return <Navigate to="/finance/overview" replace />;
  if (role === "PartnerSuccessManager") return <Navigate to="/modules/delivery-service/catalog" replace />;
  return <Navigate to="/dashboard" replace />;
}

export function AdminRoutes({ lang }: { lang: Lang }) {
  return (
    <Routes>
      <Route path="/" element={<HomeRedirect />} />

      <Route
        path="/dashboard"
        element={
          <RequirePermission permissions={[PortalPermissions.Dashboard.Read]}>
            <DashboardPage lang={lang} />
          </RequirePermission>
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

      <Route path="/users" element={<Navigate to="/settings/users" replace />} />

      {/* Legacy flat routes → module or finance equivalents */}
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
  );
}
