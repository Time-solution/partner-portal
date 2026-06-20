import { useCallback, useEffect, useState } from "react";
import { Loader2, RotateCcw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import { resetMockPortalDataSourceInstance } from "@/lib/data/mockDataSource";
import type { AuditEntry, PortalTeam } from "@/lib/data/types";
import { PORTAL_ROLES, roleLabels, rolePermissionMap, PortalPermissions } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

export function SettingsPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { can } = usePortalSession();
  const [teams, setTeams] = useState<PortalTeam[]>([]);
  const [audit, setAudit] = useState<AuditEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [resetting, setResetting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [tms, aud] = await Promise.all([ds.getTeams(), ds.getAuditLog()]);
      setTeams(tms);
      setAudit(aud);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const resetDemo = async () => {
    setResetting(true);
    try {
      await getPortalDataSource().resetDemoData();
      resetMockPortalDataSourceInstance();
      await load();
    } finally {
      setResetting(false);
    }
  };

  const canReset = can(PortalPermissions.Settings.Manage);

  return (
    <div className="space-y-6">
      <PageHeader title={t("navSettings" as never)} description={t("settingsDesc" as never)} lang={lang} />

      {canReset ? (
        <Card className="border-warning/30">
          <CardHeader>
            <CardTitle className="text-lg">{t("settingsResetTitle" as never)}</CardTitle>
            <CardDescription>{t("settingsResetDesc" as never)}</CardDescription>
          </CardHeader>
          <CardContent>
            <Button variant="destructive" disabled={resetting} onClick={() => void resetDemo()}>
              <RotateCcw className="h-4 w-4" />
              {t("settingsResetButton" as never)}
            </Button>
          </CardContent>
        </Card>
      ) : null}

      {loading ? (
        <div className="flex items-center gap-2 text-base text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          {t("loadingData" as never)}
        </div>
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">{t("settingsRolesTitle" as never)}</CardTitle>
              <CardDescription>{t("settingsRolesDesc" as never)}</CardDescription>
            </CardHeader>
            <CardContent>
              <ul className="divide-y divide-border text-base">
                {PORTAL_ROLES.map((role) => (
                  <li key={role} className="py-3">
                    <p className="font-medium">{roleLabels[role][lang]}</p>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {rolePermissionMap[role].slice(0, 5).join(", ")}
                      {rolePermissionMap[role].length > 5 ? "…" : ""}
                    </p>
                  </li>
                ))}
              </ul>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">{t("settingsTeamsTitle" as never)}</CardTitle>
              <CardDescription>{t("settingsTeamsDesc" as never)}</CardDescription>
            </CardHeader>
            <CardContent>
              <ul className="divide-y divide-border text-base">
                {teams.map((team) => (
                  <li key={team.id} className="flex justify-between py-3">
                    <span>
                      {team.name}
                      <span className="ms-2 rounded bg-muted px-2 py-0.5 text-sm">{team.kind}</span>
                    </span>
                    <span className="text-muted-foreground">
                      {team.memberCount} {t("settingsMembers" as never)}
                    </span>
                  </li>
                ))}
              </ul>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">{t("navAudit" as never)}</CardTitle>
              <CardDescription>{t("settingsAuditDesc" as never)}</CardDescription>
            </CardHeader>
            <CardContent>
              <ul className="divide-y divide-border text-base">
                {audit.map((entry) => (
                  <li key={entry.id} className="py-3">
                    <p className="font-medium">{entry.action}</p>
                    <p className="text-sm text-muted-foreground">
                      {entry.actor} ({entry.role}) · {entry.target} ·{" "}
                      {new Date(entry.at).toLocaleString(lang === "ar" ? "ar-SA" : "en-GB")}
                    </p>
                  </li>
                ))}
              </ul>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
