import { useCallback, useMemo, useState } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";
import { Building2, LogOut, Menu, Moon, ShieldOff, Store, Sun, Users, X } from "lucide-react";
import { OrgPermissions } from "@/lib/org/orgModel";
import { Button } from "@/components/ui/button";
import { LanguageToggle } from "@/components/LanguageToggle";
import { SessionIdleWarning } from "@/features/auth/SessionIdleWarning";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { useSessionIdleLock } from "@/hooks/useSessionIdleLock";
import { isMockDataSource } from "@/lib/data/config";
import { isPathActive } from "@/lib/ui/tabs";
import { useTranslator, type Lang } from "@/lib/i18n";
import { PORTAL_ROLES, type PortalRole } from "@/lib/rbac/portalRoles";
import { defaultLandingPath, roleExperience } from "@/lib/rbac/roleNavConfig";
import { navForRole } from "./nav";
import { AdminRoutes } from "./AdminRoutes";
import { BrandLogo } from "@/components/brand/BrandLogo";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { MockDataBanner } from "./components/MockDataBanner";
import { MockRoleSwitcher } from "./components/MockRoleSwitcher";
import { RoleBadge } from "./components/RoleBadge";

interface AdminShellProps {
  lang: Lang;
  toggleLang: () => void;
  theme: "light" | "dark";
  toggleTheme: () => void;
}

/** Normalize a merchant tab param the same way MerchantPreviewPage does (legacy aliases keep working). */
function normalizeMerchantTab(tab: string | null): string {
  if (tab === "partners") return "services";
  return tab ?? "dashboard";
}

function navItemActive(
  item: { key: string; path: string },
  pathname: string,
  search: string,
  scopedPartnerId?: string,
): boolean {
  // Merchant sidebar entries share one pathname and differ by ?tab= — compare params, not paths.
  if (item.key.startsWith("merchant-") && item.key !== "merchant-preview") {
    if (!pathname.startsWith("/merchant-preview")) return false;
    const itemTab = new URLSearchParams(item.path.split("?")[1] ?? "").get("tab");
    const currentTab = new URLSearchParams(search).get("tab");
    return normalizeMerchantTab(itemTab) === normalizeMerchantTab(currentTab);
  }
  if (item.key === "merchant-preview") {
    return pathname.startsWith("/merchant-preview");
  }
  if (item.key === "partner-home" && scopedPartnerId) {
    return (
      pathname === `/partners/${scopedPartnerId}` ||
      pathname === `/partners/${scopedPartnerId}/active-merchants`
    );
  }
  // Shared with the Finance tab bar so the sidebar Reports entry and the Finance
  // Reports tab share one "you are here" rule (and never both highlight Finance + Reports).
  return isPathActive(pathname, item.path);
}

export function AdminShell({ lang, toggleLang, theme, toggleTheme }: AdminShellProps) {
  const t = useTranslator(lang);
  const navigate = useNavigate();
  const { user, role, scopedPartnerId, setRole, logout, org, orgCan } = usePortalSession();
  const location = useLocation();
  const experience = roleExperience(role as PortalRole);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

  const visibleNav = useMemo(() => {
    const base = navForRole(role as PortalRole, user?.permissions ?? [], scopedPartnerId);
    const orgItems: { key: string; path: string; labelKey: string; icon: typeof Building2 }[] = [];
    if (orgCan?.(OrgPermissions.MerchantsView)) {
      orgItems.push({ key: "merchants", path: "/merchants", labelKey: "navMerchants", icon: Store });
    }
    if (orgCan?.(OrgPermissions.OrgAccountsCreate)) {
      orgItems.push({ key: "orgs", path: "/orgs", labelKey: "navOrgs", icon: Building2 });
    }
    if (org && org.level !== "Platform" && orgCan?.(OrgPermissions.UsersManage)) {
      orgItems.push({ key: "org-users", path: "/org-users", labelKey: "navOrgUsers", icon: Users });
    }
    return [...base, ...orgItems];
  }, [user, role, scopedPartnerId, org, orgCan]);

  const handleRoleChange = useCallback(
    (next: PortalRole) => {
      setRole?.(next);
      navigate(defaultLandingPath(next), { replace: true });
    },
    [navigate, setRole],
  );

  const activeLabel = useMemo(() => {
    const match = visibleNav.find((item) =>
      navItemActive(item, location.pathname, location.search, scopedPartnerId),
    );
    return match ? t(match.labelKey as never) : t("adminConsole");
  }, [location.pathname, location.search, visibleNav, scopedPartnerId, t]);

  const handleLogout = useCallback(() => {
    void logout();
  }, [logout]);

  const { showWarning, remainingMs, staySignedIn } = useSessionIdleLock(handleLogout);

  const header = (
    <header className="flex h-16 items-center justify-between gap-3 border-b border-border bg-card px-4 md:px-6">
      <div className="flex min-w-0 items-center gap-3">
        <button
          type="button"
          className="rounded-md p-2 text-muted-foreground hover:bg-muted hover:text-foreground md:hidden"
          aria-label={t("navOpenMenu" as never)}
          data-testid="mobile-nav-toggle"
          onClick={() => setMobileNavOpen(true)}
        >
          <Menu className="h-5 w-5" aria-hidden="true" />
        </button>
        <BrandLogo variant="full" lang={lang} className="md:hidden shrink-0 max-w-[120px]" />
        <h1 className="truncate text-xl font-semibold">{activeLabel}</h1>
        {PORTAL_ROLES.includes(role as PortalRole) || role === "MerchantPreview" ? (
          <RoleBadge role={role as PortalRole} lang={lang} />
        ) : null}
        {experience === "merchant" ? (
          <span className="hidden rounded-full border border-teal-500/30 bg-teal-500/10 px-2 py-0.5 text-xs text-teal-700 dark:text-teal-300 sm:inline">
            {t("merchantPreviewBadge" as never)}
          </span>
        ) : (
          <BetaBadge lang={lang} className="hidden sm:inline-flex" />
        )}
      </div>

      <div className="flex items-center gap-2">
        {isMockDataSource() && setRole ? (
          <MockRoleSwitcher role={role as PortalRole} onChange={handleRoleChange} lang={lang} />
        ) : null}

        {experience === "admin" ? (
          <span className="hidden items-center gap-1.5 rounded-full border border-border px-2.5 py-1 text-xs text-muted-foreground sm:flex">
            <ShieldOff className="h-3.5 w-3.5" aria-hidden="true" />
            {t("mfaDisabled")}
          </span>
        ) : null}

        <LanguageToggle lang={lang} toggleLang={toggleLang} />

        <Button variant="outline" size="icon" onClick={toggleTheme} aria-label={t("toggleTheme")}>
          {theme === "dark" ? (
            <Sun className="h-4 w-4" aria-hidden="true" />
          ) : (
            <Moon className="h-4 w-4" aria-hidden="true" />
          )}
        </Button>

        <Button variant="outline" size="sm" onClick={handleLogout}>
          <LogOut className="h-4 w-4" aria-hidden="true" />
          {t("signOut")}
        </Button>
      </div>
    </header>
  );

  const main = (
    <main className="flex-1 space-y-5 p-5 md:p-8">
      <MockDataBanner lang={lang} />
      <div className="text-base text-muted-foreground">
        {t("signedInAs")}{" "}
        <span className="font-medium text-foreground">{user?.name ?? user?.username}</span>
      </div>
      <AdminRoutes lang={lang} />
    </main>
  );

  // One nav list rendered in both the desktop aside and the mobile drawer. Active-state is
  // param-aware (merchant tabs) and partner-scope-aware; NavLink's own isActive is NOT used
  // because merchant entries share a pathname.
  const navList = (
    <nav className="flex-1 space-y-1 overflow-y-auto p-3" aria-label={t("adminConsole")}>
      {visibleNav.map((item) => {
        const Icon = item.icon;
        const active = navItemActive(item, location.pathname, location.search, scopedPartnerId);
        return (
          <NavLink
            key={item.key}
            to={item.path}
            data-testid={`nav-${item.key}`}
            onClick={() => setMobileNavOpen(false)}
            className={[
              "flex w-full items-center gap-3 rounded-md px-3 py-2.5 text-base font-medium transition-colors",
              "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
              active
                ? "bg-primary text-primary-foreground shadow-sm"
                : "text-muted-foreground hover:bg-muted hover:text-foreground",
            ].join(" ")}
          >
            <Icon className="h-5 w-5" aria-hidden="true" />
            {t(item.labelKey as never)}
          </NavLink>
        );
      })}
    </nav>
  );

  // RTL-first: the sidebar sits on the inline-START side (logical border-e) and mirrors with dir.
  return (
    <div className="flex min-h-screen bg-background text-foreground" dir={lang === "ar" ? "rtl" : "ltr"}>
      <aside
        data-testid="app-sidebar"
        className="hidden w-64 shrink-0 flex-col border-e border-border bg-card md:flex"
      >
        <div className="flex h-16 items-center gap-2 border-b border-border px-4 md:px-6">
          <BrandLogo variant="full" lang={lang} className="shrink-0" />
          <BetaBadge lang={lang} className="ms-auto" />
        </div>
        {navList}
      </aside>

      {mobileNavOpen ? (
        <div className="fixed inset-0 z-50 md:hidden" role="presentation">
          <div className="absolute inset-0 bg-black/50" onClick={() => setMobileNavOpen(false)} aria-hidden="true" />
          <div
            data-testid="mobile-nav-drawer"
            className="absolute inset-y-0 start-0 flex w-72 flex-col border-e border-border bg-card shadow-xl"
          >
            <div className="flex h-16 items-center justify-between gap-2 border-b border-border px-4">
              <BrandLogo variant="full" lang={lang} className="shrink-0 max-w-[140px]" />
              <button
                type="button"
                className="rounded-md p-2 text-muted-foreground hover:bg-muted hover:text-foreground"
                aria-label={t("navOpenMenu" as never)}
                onClick={() => setMobileNavOpen(false)}
              >
                <X className="h-4 w-4" aria-hidden="true" />
              </button>
            </div>
            {navList}
          </div>
        </div>
      ) : null}

      <div className="flex min-w-0 flex-1 flex-col">
        {header}
        {main}
      </div>

      {showWarning ? (
        <SessionIdleWarning lang={lang} remainingMs={remainingMs} onStaySignedIn={staySignedIn} />
      ) : null}
    </div>
  );
}
