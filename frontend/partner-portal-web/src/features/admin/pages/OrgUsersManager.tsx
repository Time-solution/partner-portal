import { useCallback, useEffect, useMemo, useState } from "react";
import { Loader2, Pencil, ShieldCheck, UserCheck, UserMinus, UserPlus, Users } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getPortalDataSource } from "@/lib/data";
import type { CreateOrgUserInput, Org, OrgUser } from "@/lib/data/types";
import {
  OrgPermissions,
  PERMISSION_CATALOG,
  permissionsForLevel,
  presetPermissions,
  presetsForLevel,
  sanitizePermissionsForLevel,
  type OrgLevel,
  type OrgPermission,
} from "@/lib/org/orgModel";
import { usePortalSession } from "@/features/auth/usePortalSession";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

const statusClass: Record<string, string> = {
  Active: "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300",
  Invited: "bg-amber-500/15 text-amber-800 dark:text-amber-200",
  Suspended: "bg-muted text-muted-foreground",
};

const GROUP_LABEL_KEY: Record<string, string> = {
  Partners: "permGroupPartners",
  Activations: "permGroupActivations",
  Finance: "permGroupFinance",
  Flows: "permGroupFlows",
  Catalog: "permGroupCatalog",
  Integration: "permGroupIntegration",
  Administration: "permGroupAdministration",
};

interface FormState {
  name: string;
  email: string;
  rolePreset: string;
  permissions: OrgPermission[];
}

function groupedCatalog(level: OrgLevel) {
  const entries = PERMISSION_CATALOG.filter((p) => p.levels.includes(level));
  const groups = new Map<string, OrgPermission[]>();
  for (const e of entries) {
    const list = groups.get(e.group) ?? [];
    list.push(e.key);
    groups.set(e.group, list);
  }
  return [...groups.entries()];
}

export function OrgUsersManager({ lang, org }: { lang: Lang; org: Org }) {
  const t = useTranslator(lang);
  const { orgCan } = usePortalSession();
  const canManage = orgCan ? orgCan(OrgPermissions.UsersManage) : false;

  const level = org.level;
  const presets = useMemo(() => presetsForLevel(level), [level]);
  const catalog = useMemo(() => groupedCatalog(level), [level]);

  const emptyForm = useCallback((): FormState => {
    const preset = presets[0]?.key ?? "";
    return { name: "", email: "", rolePreset: preset, permissions: presetPermissions(level, preset) };
  }, [presets, level]);

  const [users, setUsers] = useState<OrgUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [showAdd, setShowAdd] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setUsers(await getPortalDataSource().listOrgUsers(org.id));
    } finally {
      setLoading(false);
    }
  }, [org.id]);

  useEffect(() => {
    void load();
  }, [load]);

  const startAdd = () => {
    setEditingId(null);
    setForm(emptyForm());
    setError(null);
    setShowAdd(true);
  };

  const startEdit = (user: OrgUser) => {
    setShowAdd(false);
    setEditingId(user.id);
    setForm({
      name: user.name,
      email: user.email,
      rolePreset: user.rolePreset,
      permissions: sanitizePermissionsForLevel(level, user.permissions),
    });
    setError(null);
  };

  const onPresetChange = (presetKey: string) => {
    setForm((f) => ({ ...f, rolePreset: presetKey, permissions: presetPermissions(level, presetKey) }));
  };

  const togglePermission = (perm: OrgPermission) => {
    setForm((f) => ({
      ...f,
      permissions: f.permissions.includes(perm)
        ? f.permissions.filter((p) => p !== perm)
        : [...f.permissions, perm],
    }));
  };

  const submit = async () => {
    setError(null);
    const ds = getPortalDataSource();
    const payload: CreateOrgUserInput = {
      name: form.name.trim(),
      email: form.email.trim(),
      rolePreset: form.rolePreset,
      permissions: sanitizePermissionsForLevel(level, form.permissions),
    };
    setBusyId(editingId ?? "add");
    try {
      if (editingId) {
        await ds.updateOrgUser(org.id, editingId, payload);
        setEditingId(null);
      } else {
        await ds.createOrgUser(org.id, payload);
        setShowAdd(false);
      }
      setForm(emptyForm());
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    } finally {
      setBusyId(null);
    }
  };

  const setStatus = async (userId: string, status: OrgUser["status"]) => {
    setBusyId(userId);
    try {
      if (status === "Suspended") await getPortalDataSource().suspendOrgUser(org.id, userId);
      else await getPortalDataSource().updateOrgUser(org.id, userId, { status });
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const permLabel = (key: OrgPermission) => t(`perm.${key}` as never);

  const formCard = (title: string, icon: React.ReactNode) => (
    <Card className={editingId ? "border-primary/30" : undefined}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-lg">
          {icon}
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1">
            <Label htmlFor="ou-name">{t("orgUserName" as never)}</Label>
            <Input id="ou-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="ou-email">{t("orgUserEmail" as never)}</Label>
            <Input
              id="ou-email"
              type="email"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="ou-role">{t("orgUserRole" as never)}</Label>
            <select
              id="ou-role"
              value={form.rolePreset}
              onChange={(e) => onPresetChange(e.target.value)}
              className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {presets.map((p) => (
                <option key={p.key} value={p.key}>
                  {p.key}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="space-y-3 rounded-md border border-border p-4">
          <div className="flex items-center gap-2 text-sm font-medium">
            <ShieldCheck className="h-4 w-4 text-primary" aria-hidden="true" />
            {t("permissionsTitle" as never)}
            <span className="font-normal text-muted-foreground">— {t("permissionsDescription" as never)}</span>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            {catalog.map(([group, perms]) => (
              <fieldset key={group} className="space-y-1.5">
                <legend className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  {t(GROUP_LABEL_KEY[group] as never)}
                </legend>
                {perms.map((perm) => (
                  <label key={perm} className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      className="h-4 w-4 rounded border-input"
                      checked={form.permissions.includes(perm)}
                      onChange={() => togglePermission(perm)}
                    />
                    {permLabel(perm)}
                  </label>
                ))}
              </fieldset>
            ))}
          </div>
        </div>

        {error ? <p className="text-sm text-destructive">{error}</p> : null}
        <div className="flex flex-wrap gap-2">
          <Button disabled={busyId === (editingId ?? "add")} onClick={() => void submit()}>
            {busyId === (editingId ?? "add") ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            {t("orgUserSaved" as never)}
          </Button>
          <Button
            variant="outline"
            onClick={() => {
              setShowAdd(false);
              setEditingId(null);
            }}
          >
            {t("usersCancel" as never)}
          </Button>
        </div>
      </CardContent>
    </Card>
  );

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-base text-muted-foreground">{t("orgUsersDescription" as never)}</p>
        {canManage ? (
          <Button size="sm" onClick={startAdd}>
            <UserPlus className="h-4 w-4" />
            {t("orgUserAdd" as never)}
          </Button>
        ) : null}
      </div>

      {showAdd && canManage
        ? formCard(t("orgUserAdd" as never), <UserPlus className="h-5 w-5 text-primary" aria-hidden="true" />)
        : null}
      {editingId && canManage
        ? formCard(t("orgUserEdit" as never), <Pencil className="h-5 w-5 text-primary" aria-hidden="true" />)
        : null}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            <Users className="h-5 w-5 text-primary" aria-hidden="true" />
            {t("orgUsersList" as never)}
          </CardTitle>
          <CardDescription>{org.name}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-base text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[640px] text-left text-base">
                <thead>
                  <tr className="border-b border-border text-sm text-muted-foreground">
                    <th className="pb-3 pe-4 font-medium">{t("orgUserName" as never)}</th>
                    <th className="pb-3 pe-4 font-medium">{t("orgUserEmail" as never)}</th>
                    <th className="pb-3 pe-4 font-medium">{t("orgUserRole" as never)}</th>
                    <th className="pb-3 pe-4 font-medium">{t("orgUserStatus" as never)}</th>
                    {canManage ? <th className="pb-3 font-medium">{t("usersColActions" as never)}</th> : null}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {users.map((user) => (
                    <tr key={user.id}>
                      <td className="py-3 pe-4 font-medium">{user.name}</td>
                      <td className="py-3 pe-4 text-sm text-muted-foreground">{user.email}</td>
                      <td className="py-3 pe-4">
                        <span className="text-sm">{user.rolePreset}</span>
                        <span className="block text-xs text-muted-foreground">
                          {user.permissions.length} / {permissionsForLevel(level).length}
                        </span>
                      </td>
                      <td className="py-3 pe-4">
                        <span
                          className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${statusClass[user.status] ?? ""}`}
                        >
                          {t(`orgUserStatus${user.status}` as never)}
                        </span>
                      </td>
                      {canManage ? (
                        <td className="py-3">
                          <div className="flex flex-wrap gap-1">
                            <Button size="sm" variant="outline" disabled={busyId === user.id} onClick={() => startEdit(user)}>
                              <Pencil className="h-3.5 w-3.5" />
                              {t("orgUserEdit" as never)}
                            </Button>
                            {user.status !== "Suspended" ? (
                              <Button
                                size="sm"
                                variant="outline"
                                disabled={busyId === user.id}
                                onClick={() => void setStatus(user.id, "Suspended")}
                              >
                                <UserMinus className="h-3.5 w-3.5" />
                                {t("orgUserSuspend" as never)}
                              </Button>
                            ) : (
                              <Button
                                size="sm"
                                variant="outline"
                                disabled={busyId === user.id}
                                onClick={() => void setStatus(user.id, "Active")}
                              >
                                <UserCheck className="h-3.5 w-3.5" />
                                {t("orgUserReactivate" as never)}
                              </Button>
                            )}
                          </div>
                        </td>
                      ) : null}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
