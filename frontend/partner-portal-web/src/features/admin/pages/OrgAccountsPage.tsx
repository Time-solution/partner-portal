import { useCallback, useEffect, useMemo, useState } from "react";
import { Building2, Loader2, Plus, Store, Users } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getPortalDataSource } from "@/lib/data";
import type { CreateOrgAccountInput, Org, Partner } from "@/lib/data/types";
import { OrgPermissions } from "@/lib/org/orgModel";
import { usePortalSession } from "@/features/auth/usePortalSession";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { OrgUsersManager } from "./OrgUsersManager";

const levelBadge: Record<string, string> = {
  Platform: "bg-primary/15 text-primary",
  Partner: "bg-violet-500/15 text-violet-700 dark:text-violet-300",
  Merchant: "bg-teal-500/15 text-teal-700 dark:text-teal-300",
};

export function OrgAccountsPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { orgCan } = usePortalSession();
  const canCreate = orgCan ? orgCan(OrgPermissions.OrgAccountsCreate) : false;

  const [orgs, setOrgs] = useState<Org[]>([]);
  const [partners, setPartners] = useState<Partner[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedOrgId, setSelectedOrgId] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<CreateOrgAccountInput>({
    level: "Partner",
    name: "",
    partnerId: "",
    tenantId: "",
    adminName: "",
    adminEmail: "",
  });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [orgList, partnerList] = await Promise.all([ds.listOrgs(), ds.getPartners()]);
      setOrgs(orgList);
      setPartners(partnerList);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const partnersWithoutOrg = useMemo(() => {
    const taken = new Set(orgs.filter((o) => o.level === "Partner").map((o) => o.partnerId));
    return partners.filter((p) => !taken.has(p.id));
  }, [orgs, partners]);

  const selectedOrg = orgs.find((o) => o.id === selectedOrgId) ?? null;

  const submitCreate = async () => {
    setError(null);
    setBusy(true);
    try {
      const input: CreateOrgAccountInput = {
        level: form.level,
        name: form.name.trim(),
        partnerId: form.level === "Partner" ? form.partnerId || undefined : undefined,
        tenantId: form.level === "Merchant" ? form.tenantId || undefined : undefined,
        adminName: form.adminName?.trim() || undefined,
        adminEmail: form.adminEmail?.trim() || undefined,
      };
      await getPortalDataSource().createOrgAccount(input);
      setShowCreate(false);
      setForm({ level: "Partner", name: "", partnerId: "", tenantId: "", adminName: "", adminEmail: "" });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    } finally {
      setBusy(false);
    }
  };

  const levelLabel = (level: string) =>
    level === "Platform"
      ? t("orgLevelPlatform" as never)
      : level === "Partner"
        ? t("orgLevelPartner" as never)
        : t("orgLevelMerchant" as never);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-base text-muted-foreground">{t("orgsDescription" as never)}</p>
        {canCreate ? (
          <Button size="sm" onClick={() => setShowCreate((v) => !v)}>
            <Plus className="h-4 w-4" />
            {t("orgCreateAccount" as never)}
          </Button>
        ) : null}
      </div>

      {showCreate && canCreate ? (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-lg">
              <Building2 className="h-5 w-5 text-primary" aria-hidden="true" />
              {t("orgCreateTitle" as never)}
            </CardTitle>
            <CardDescription>{t("orgCreateDescription" as never)}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1">
                <Label htmlFor="org-level">{t("orgUserRole" as never)}</Label>
                <select
                  id="org-level"
                  value={form.level}
                  onChange={(e) =>
                    setForm({ ...form, level: e.target.value as CreateOrgAccountInput["level"] })
                  }
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <option value="Partner">{t("orgLevelPartner" as never)}</option>
                  <option value="Merchant">{t("orgLevelMerchant" as never)}</option>
                </select>
              </div>
              <div className="space-y-1">
                <Label htmlFor="org-name">{t("orgNameLabel" as never)}</Label>
                <Input id="org-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
              </div>
              {form.level === "Partner" ? (
                <div className="space-y-1">
                  <Label htmlFor="org-partner">{t("orgLevelPartner" as never)}</Label>
                  <select
                    id="org-partner"
                    value={form.partnerId}
                    onChange={(e) => {
                      const p = partners.find((x) => x.id === e.target.value);
                      setForm({ ...form, partnerId: e.target.value, name: form.name || (p ? (p.tradeName ?? p.legalName) : "") });
                    }}
                    className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    <option value="">—</option>
                    {partnersWithoutOrg.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.tradeName ?? p.legalName}
                      </option>
                    ))}
                  </select>
                </div>
              ) : (
                <div className="space-y-1">
                  <Label htmlFor="org-tenant">{t("orgLevelMerchant" as never)} (TenantId)</Label>
                  <Input
                    id="org-tenant"
                    value={form.tenantId}
                    onChange={(e) => setForm({ ...form, tenantId: e.target.value })}
                    placeholder="11111111-1111-1111-1111-1111111110xx"
                  />
                </div>
              )}
              <div className="space-y-1">
                <Label htmlFor="org-admin-name">{t("orgAdminNameLabel" as never)}</Label>
                <Input
                  id="org-admin-name"
                  value={form.adminName}
                  onChange={(e) => setForm({ ...form, adminName: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="org-admin-email">{t("orgAdminEmailLabel" as never)}</Label>
                <Input
                  id="org-admin-email"
                  type="email"
                  value={form.adminEmail}
                  onChange={(e) => setForm({ ...form, adminEmail: e.target.value })}
                />
              </div>
            </div>
            {error ? <p className="text-sm text-destructive">{error}</p> : null}
            <div className="flex flex-wrap gap-2">
              <Button disabled={busy} onClick={() => void submitCreate()}>
                {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                {t("orgCreateAccount" as never)}
              </Button>
              <Button variant="outline" onClick={() => setShowCreate(false)}>
                {t("usersCancel" as never)}
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            <Building2 className="h-5 w-5 text-primary" aria-hidden="true" />
            {t("orgsTitle" as never)}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-base text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : (
            <div className="grid gap-2">
              {orgs.map((org) => (
                <button
                  key={org.id}
                  type="button"
                  onClick={() => setSelectedOrgId((id) => (id === org.id ? null : org.id))}
                  className={[
                    "flex items-center justify-between gap-3 rounded-md border px-3 py-2.5 text-start transition-colors",
                    selectedOrgId === org.id ? "border-primary bg-primary/5" : "border-border hover:bg-muted",
                  ].join(" ")}
                >
                  <span className="flex items-center gap-2">
                    {org.level === "Merchant" ? (
                      <Store className="h-4 w-4 text-muted-foreground" />
                    ) : (
                      <Building2 className="h-4 w-4 text-muted-foreground" />
                    )}
                    <span className="font-medium">{org.name}</span>
                  </span>
                  <span className="flex items-center gap-2">
                    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${levelBadge[org.level] ?? ""}`}>
                      {levelLabel(org.level)}
                    </span>
                    <Users className="h-4 w-4 text-muted-foreground" />
                  </span>
                </button>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {selectedOrg ? (
        <div className="space-y-3">
          <h2 className="flex items-center gap-2 text-lg font-semibold">
            <Users className="h-5 w-5 text-primary" aria-hidden="true" />
            {t("orgUsersTitle" as never)} — {selectedOrg.name}
          </h2>
          <OrgUsersManager lang={lang} org={selectedOrg} />
        </div>
      ) : null}
    </div>
  );
}
