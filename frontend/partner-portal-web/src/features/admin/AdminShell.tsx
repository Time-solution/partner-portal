import { useMemo } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { Languages, LogOut, Moon, ShieldOff, Sun } from "lucide-react";
import { Button } from "@/components/ui/button";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { isMockDataSource } from "@/lib/data/config";
import { useTranslator, type Lang } from "@/lib/i18n";
import { PORTAL_ROLES, type PortalRole } from "@/lib/rbac/portalRoles";
import { adminNav, filterNavByPermissions } from "./nav";
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

export function AdminShell({ lang, toggleLang, theme, toggleTheme }: AdminShellProps) {
  const t = useTranslator(lang);
  const { user, role, setRole, logout } = usePortalSession();
  const location = useLocation();

  const visibleNav = useMemo(
    () => filterNavByPermissions(adminNav, user?.permissions ?? []),
    [user],
  );

  const activeLabel = useMemo(() => {
    const match = visibleNav.find((item) => location.pathname.startsWith(item.path));
    return match ? t(match.labelKey as never) : t("adminConsole");
  }, [location.pathname, visibleNav, t]);

  const handleLogout = () => {
    void logout();
  };

  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <aside className="hidden w-64 shrink-0 flex-col border-e border-border bg-card md:flex">
        <div className="flex h-16 items-center gap-2 border-b border-border px-4 md:px-6">
          <BrandLogo variant="full" lang={lang} className="shrink-0" />
          <BetaBadge lang={lang} className="ms-auto" />
        </div>

        <nav className="flex-1 space-y-1 overflow-y-auto p-3" aria-label={t("adminConsole")}>
          {visibleNav.map((item) => {
            const Icon = item.icon;
            return (
              <NavLink
                key={item.key}
                to={item.path}
                className={({ isActive }) =>
                  [
                    "flex w-full items-center gap-3 rounded-md px-3 py-2.5 text-base font-medium transition-colors",
                    "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                    isActive
                      ? "bg-primary/10 text-primary"
                      : "text-muted-foreground hover:bg-muted hover:text-foreground",
                  ].join(" ")
                }
              >
                <Icon className="h-5 w-5" aria-hidden="true" />
                {t(item.labelKey as never)}
              </NavLink>
            );
          })}
        </nav>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-16 items-center justify-between gap-3 border-b border-border bg-card px-4 md:px-6">
          <div className="flex min-w-0 items-center gap-3">
            <BrandLogo variant="full" lang={lang} className="md:hidden shrink-0 max-w-[120px]" />
            <h1 className="truncate text-xl font-semibold">{activeLabel}</h1>
            {PORTAL_ROLES.includes(role as PortalRole) ? (
              <RoleBadge role={role as PortalRole} lang={lang} />
            ) : null}
            <BetaBadge lang={lang} className="hidden sm:inline-flex" />
          </div>

          <div className="flex items-center gap-2">
            {isMockDataSource() && setRole ? (
              <MockRoleSwitcher role={role as PortalRole} onChange={setRole} lang={lang} />
            ) : null}

            <span className="hidden items-center gap-1.5 rounded-full border border-border px-2.5 py-1 text-xs text-muted-foreground sm:flex">
              <ShieldOff className="h-3.5 w-3.5" aria-hidden="true" />
              {t("mfaDisabled")}
            </span>

            <Button variant="outline" size="sm" onClick={toggleLang} aria-label={t("toggleToEnglish")}>
              <Languages className="h-4 w-4" aria-hidden="true" />
              {t("toggleToEnglish")}
            </Button>

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

        <main className="flex-1 space-y-5 p-5 md:p-8">
          <MockDataBanner lang={lang} />
          <div className="text-base text-muted-foreground">
            {t("signedInAs")}{" "}
            <span className="font-medium text-foreground">{user?.name ?? user?.username}</span>
          </div>
          <AdminRoutes lang={lang} />
        </main>
      </div>
    </div>
  );
}