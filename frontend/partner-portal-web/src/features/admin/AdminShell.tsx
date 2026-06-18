import { useMemo, useState } from "react";
import { Languages, LogOut, Moon, ShieldOff, Sun } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { useAuth } from "@/features/auth/AuthContext";
import { useSessionTimeout } from "@/hooks/useSessionTimeout";
import { useTranslator, type Lang } from "@/lib/i18n";
import { adminNav, filterNavByPermissions } from "./nav";
import { PartnersPage } from "./PartnersPage";

const SESSION_IDLE_MS = 15 * 60 * 1000; // 15 minutes of inactivity

interface AdminShellProps {
  lang: Lang;
  toggleLang: () => void;
  theme: "light" | "dark";
  toggleTheme: () => void;
}

export function AdminShell({ lang, toggleLang, theme, toggleTheme }: AdminShellProps) {
  const t = useTranslator(lang);
  const { user, logout } = useAuth();
  const [active, setActive] = useState("dashboard");

  const visibleNav = useMemo(
    () => filterNavByPermissions(adminNav, user?.permissions ?? []),
    [user],
  );

  useSessionTimeout({ timeoutMs: SESSION_IDLE_MS, onTimeout: logout });

  const activeItem = visibleNav.find((item) => item.key === active) ?? visibleNav[0];

  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <aside className="hidden w-64 shrink-0 flex-col border-e border-border bg-card md:flex">
        <div className="flex h-16 items-center gap-2 border-b border-border px-6">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <span className="text-sm font-bold">Z</span>
          </div>
          <span className="text-sm font-semibold">{t("adminConsole")}</span>
        </div>

        <nav className="flex-1 space-y-1 overflow-y-auto p-3" aria-label={t("adminConsole")}>
          {visibleNav.map((item) => {
            const Icon = item.icon;
            const isActive = item.key === activeItem?.key;
            return (
              <button
                key={item.key}
                type="button"
                onClick={() => setActive(item.key)}
                aria-current={isActive ? "page" : undefined}
                className={[
                  "flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                  isActive
                    ? "bg-primary/10 text-primary"
                    : "text-muted-foreground hover:bg-muted hover:text-foreground",
                ].join(" ")}
              >
                <Icon className="h-4 w-4" aria-hidden="true" />
                {t(item.labelKey as never)}
              </button>
            );
          })}
        </nav>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-16 items-center justify-between gap-3 border-b border-border bg-card px-4 md:px-6">
          <h1 className="truncate text-lg font-semibold">
            {activeItem ? t(activeItem.labelKey as never) : t("adminConsole")}
          </h1>

          <div className="flex items-center gap-2">
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

            <Button variant="outline" size="sm" onClick={logout}>
              <LogOut className="h-4 w-4" aria-hidden="true" />
              {t("signOut")}
            </Button>
          </div>
        </header>

        <main className="flex-1 space-y-6 p-4 md:p-8">
          <div className="text-sm text-muted-foreground">
            {t("signedInAs")}{" "}
            <span className="font-medium text-foreground">{user?.name ?? user?.username}</span>
          </div>

          {activeItem?.key === "partners" ? (
            <PartnersPage lang={lang} />
          ) : (
            <Card>
              <CardHeader>
                <CardTitle>{activeItem ? t(activeItem.labelKey as never) : ""}</CardTitle>
                <CardDescription>{t("comingSoon")}</CardDescription>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">{t("sectionPlaceholder")}</p>
              </CardContent>
            </Card>
          )}
        </main>
      </div>
    </div>
  );
}
