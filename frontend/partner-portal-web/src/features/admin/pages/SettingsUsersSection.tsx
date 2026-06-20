import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Loader2,
  Mail,
  Pencil,
  Plus,
  UserCheck,
  UserMinus,
  UserPlus,
  Users,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getPortalDataSource } from "@/lib/data";
import type { CreatePortalUserInput, Partner, PortalUser, PortalUserRole } from "@/lib/data/types";
import {
  PORTAL_ROLES,
  PortalPermissions,
  canGrantRole,
  roleLabels,
  roleRequiresPartner,
  type PortalRole,
} from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

const statusClass: Record<string, string> = {
  Active: "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300",
  Invited: "bg-amber-500/15 text-amber-800 dark:text-amber-200",
  Suspended: "bg-muted text-muted-foreground",
};

interface UserFormState {
  name: string;
  email: string;
  role: PortalUserRole;
  partnerId: string;
}

const emptyForm = (): UserFormState => ({
  name: "",
  email: "",
  role: "Accountant",
  partnerId: "",
});

function userToForm(user: PortalUser): UserFormState {
  return {
    name: user.name,
    email: user.email,
    role: user.roles[0] ?? "Accountant",
    partnerId: user.partnerId ?? "",
  };
}

function isValidEmail(email: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
}

/** User management section — lives under Settings (/settings/users). */
export function SettingsUsersSection({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { role: actorRole, can } = usePortalSession();
  const canManage = can(PortalPermissions.Users.Manage);
  const canView = can(PortalPermissions.Users.Read);

  const [users, setUsers] = useState<PortalUser[]>([]);
  const [partners, setPartners] = useState<Partner[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [showAddForm, setShowAddForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [addForm, setAddForm] = useState<UserFormState>(emptyForm);
  const [editForm, setEditForm] = useState<UserFormState>(emptyForm);
  const [formError, setFormError] = useState<string | null>(null);

  const assignableRoles = useMemo(
    () =>
      PORTAL_ROLES.filter((r) => canGrantRole(actorRole as PortalRole, r)) as PortalUserRole[],
    [actorRole],
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [userList, partnerList] = await Promise.all([ds.listUsers(), ds.getPartners()]);
      setUsers(userList);
      setPartners(partnerList.filter((p) => p.status === "Active"));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  if (!canView) {
    return (
      <p className="text-sm text-muted-foreground">{t("usersHidden" as never)}</p>
    );
  }

  const submitAdd = async () => {
    setFormError(null);
    if (!addForm.name.trim()) {
      setFormError(t("usersErrorName" as never));
      return;
    }
    if (!isValidEmail(addForm.email)) {
      setFormError(t("usersErrorEmail" as never));
      return;
    }
    if (roleRequiresPartner(addForm.role) && !addForm.partnerId) {
      setFormError(t("usersErrorPartner" as never));
      return;
    }
    if (!canGrantRole(actorRole as PortalRole, addForm.role)) {
      setFormError(t("usersErrorRoleEscalation" as never));
      return;
    }

    setBusyId("add");
    try {
      const input: CreatePortalUserInput = {
        name: addForm.name.trim(),
        email: addForm.email.trim(),
        role: addForm.role,
        partnerId: roleRequiresPartner(addForm.role) ? addForm.partnerId : undefined,
      };
      await getPortalDataSource().createUser(input);
      setAddForm(emptyForm());
      setShowAddForm(false);
      await load();
    } catch (err) {
      setFormError(err instanceof Error ? err.message : t("usersErrorGeneric" as never));
    } finally {
      setBusyId(null);
    }
  };

  const startEdit = (user: PortalUser) => {
    setEditingId(user.id);
    setEditForm(userToForm(user));
    setFormError(null);
  };

  const submitEdit = async () => {
    if (!editingId) return;
    setFormError(null);
    if (!editForm.name.trim()) {
      setFormError(t("usersErrorName" as never));
      return;
    }
    if (!isValidEmail(editForm.email)) {
      setFormError(t("usersErrorEmail" as never));
      return;
    }
    if (roleRequiresPartner(editForm.role) && !editForm.partnerId) {
      setFormError(t("usersErrorPartner" as never));
      return;
    }
    if (!canGrantRole(actorRole as PortalRole, editForm.role)) {
      setFormError(t("usersErrorRoleEscalation" as never));
      return;
    }

    setBusyId(editingId);
    try {
      await getPortalDataSource().updateUser(editingId, {
        name: editForm.name.trim(),
        email: editForm.email.trim(),
        role: editForm.role,
        partnerId: roleRequiresPartner(editForm.role) ? editForm.partnerId : null,
      });
      setEditingId(null);
      await load();
    } catch (err) {
      setFormError(err instanceof Error ? err.message : t("usersErrorGeneric" as never));
    } finally {
      setBusyId(null);
    }
  };

  const setStatus = async (userId: string, status: PortalUser["status"]) => {
    setBusyId(userId);
    try {
      await getPortalDataSource().updateUser(userId, { status });
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const resendInvite = async (userId: string) => {
    setBusyId(`invite-${userId}`);
    try {
      await getPortalDataSource().updateUser(userId, { resendInvite: true });
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const roleLabel = (r: PortalUserRole) => roleLabels[r][lang];

  const partnerSelect = (
    id: string,
    value: string,
    onChange: (v: string) => void,
    required: boolean,
  ) => (
    <div className="space-y-1">
      <Label htmlFor={id}>{t("usersColPartner" as never)}</Label>
      <select
        id={id}
        value={value}
        required={required}
        onChange={(e) => onChange(e.target.value)}
        className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <option value="">{t("usersSelectPartner" as never)}</option>
        {partners.map((p) => (
          <option key={p.id} value={p.id}>
            {p.tradeName ?? p.legalName}
          </option>
        ))}
      </select>
    </div>
  );

  const userFormFields = (
    form: UserFormState,
    setForm: (next: UserFormState) => void,
    prefix: string,
  ) => (
    <div className="grid gap-4 sm:grid-cols-2">
      <div className="space-y-1">
        <Label htmlFor={`${prefix}-name`}>{t("usersColName" as never)}</Label>
        <Input
          id={`${prefix}-name`}
          value={form.name}
          onChange={(e) => setForm({ ...form, name: e.target.value })}
          required
        />
      </div>
      <div className="space-y-1">
        <Label htmlFor={`${prefix}-email`}>{t("usersColEmail" as never)}</Label>
        <Input
          id={`${prefix}-email`}
          type="email"
          value={form.email}
          onChange={(e) => setForm({ ...form, email: e.target.value })}
          required
        />
      </div>
      <div className="space-y-1">
        <Label htmlFor={`${prefix}-role`}>{t("usersColRole" as never)}</Label>
        <select
          id={`${prefix}-role`}
          value={form.role}
          onChange={(e) =>
            setForm({
              ...form,
              role: e.target.value as PortalUserRole,
              partnerId: roleRequiresPartner(e.target.value as PortalUserRole) ? form.partnerId : "",
            })
          }
          className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {assignableRoles.map((r) => (
            <option key={r} value={r}>
              {roleLabel(r)}
            </option>
          ))}
        </select>
      </div>
      {roleRequiresPartner(form.role)
        ? partnerSelect(
            `${prefix}-partner`,
            form.partnerId,
            (partnerId) => setForm({ ...form, partnerId }),
            true,
          )
        : null}
    </div>
  );

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-base text-muted-foreground">{t("usersDesc" as never)}</p>
        {canManage ? (
          <Button size="sm" onClick={() => setShowAddForm((v) => !v)}>
            <Plus className="h-4 w-4" />
            {t("usersAddButton" as never)}
          </Button>
        ) : null}
      </div>

      {!canManage ? (
        <p className="text-sm text-muted-foreground">{t("usersReadOnly" as never)}</p>
      ) : null}

      {showAddForm && canManage ? (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-lg">
              <UserPlus className="h-5 w-5 text-primary" aria-hidden="true" />
              {t("usersAddTitle" as never)}
            </CardTitle>
            <CardDescription>{t("usersAddDesc" as never)}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {userFormFields(addForm, setAddForm, "add")}
            {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
            <div className="flex flex-wrap gap-2">
              <Button disabled={busyId === "add"} onClick={() => void submitAdd()}>
                {busyId === "add" ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                {t("usersAddSubmit" as never)}
              </Button>
              <Button variant="outline" onClick={() => setShowAddForm(false)}>
                {t("usersCancel" as never)}
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {editingId && canManage ? (
        <Card className="border-primary/30">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-lg">
              <Pencil className="h-5 w-5 text-primary" aria-hidden="true" />
              {t("usersEditTitle" as never)}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {userFormFields(editForm, setEditForm, "edit")}
            {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
            <div className="flex flex-wrap gap-2">
              <Button disabled={busyId === editingId} onClick={() => void submitEdit()}>
                {busyId === editingId ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                {t("usersSave" as never)}
              </Button>
              <Button variant="outline" onClick={() => setEditingId(null)}>
                {t("usersCancel" as never)}
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            <Users className="h-5 w-5 text-primary" aria-hidden="true" />
            {t("usersListTitle" as never)}
          </CardTitle>
          <CardDescription>{t("usersListDesc" as never)}</CardDescription>
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
                    <th className="pb-3 pe-4 font-medium">{t("usersColName" as never)}</th>
                    <th className="pb-3 pe-4 font-medium">{t("usersColEmail" as never)}</th>
                    <th className="pb-3 pe-4 font-medium">{t("usersColRole" as never)}</th>
                    <th className="pb-3 pe-4 font-medium">{t("usersColPartner" as never)}</th>
                    <th className="pb-3 pe-4 font-medium">{t("usersColStatus" as never)}</th>
                    {canManage ? (
                      <th className="pb-3 font-medium">{t("usersColActions" as never)}</th>
                    ) : null}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {users.map((user) => (
                    <tr key={user.id}>
                      <td className="py-3 pe-4 font-medium">{user.name}</td>
                      <td className="py-3 pe-4 text-sm text-muted-foreground">{user.email}</td>
                      <td className="py-3 pe-4">{roleLabel(user.roles[0] ?? "Accountant")}</td>
                      <td className="py-3 pe-4 text-sm">{user.partnerName ?? "—"}</td>
                      <td className="py-3 pe-4">
                        <span
                          className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${statusClass[user.status] ?? ""}`}
                        >
                          {t(`usersStatus${user.status}` as never)}
                        </span>
                        {user.status === "Invited" ? (
                          <p className="mt-1 text-xs text-muted-foreground">
                            {t("usersInvitedHint" as never)}
                          </p>
                        ) : null}
                      </td>
                      {canManage ? (
                        <td className="py-3">
                          <div className="flex flex-wrap gap-1">
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={busyId === user.id}
                              onClick={() => startEdit(user)}
                            >
                              <Pencil className="h-3.5 w-3.5" />
                            </Button>
                            {user.status !== "Suspended" ? (
                              <Button
                                size="sm"
                                variant="outline"
                                disabled={busyId === user.id}
                                onClick={() => void setStatus(user.id, "Suspended")}
                              >
                                <UserMinus className="h-3.5 w-3.5" />
                                {t("usersSuspend" as never)}
                              </Button>
                            ) : (
                              <Button
                                size="sm"
                                variant="outline"
                                disabled={busyId === user.id}
                                onClick={() => void setStatus(user.id, "Active")}
                              >
                                <UserCheck className="h-3.5 w-3.5" />
                                {t("usersReactivate" as never)}
                              </Button>
                            )}
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={busyId === `invite-${user.id}`}
                              onClick={() => void resendInvite(user.id)}
                            >
                              <Mail className="h-3.5 w-3.5" />
                              {t("usersResendInvite" as never)}
                            </Button>
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
