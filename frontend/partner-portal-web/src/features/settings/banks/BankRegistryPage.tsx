import { useCallback, useEffect, useMemo, useState } from "react";
import { Loader2, Pencil, Plus, Power, PowerOff, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { useTranslator, type Lang } from "@/lib/i18n";
import {
  BANK_PARENT_CODE,
  maskAccountNumber,
  type BankAccount,
  type BankAccountInput,
} from "@/lib/banks/bankAccount";
import { bankBalances, type BankPaymentRef } from "@/lib/banks/bankLedger";

const EMPTY_FORM: BankAccountInput = { name: "", accountNumber: "", currency: "SAR", gatewayMapping: "" };

export function BankRegistryPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { can } = usePortalSession();
  const canManage = can(PortalPermissions.Banks.Manage);

  const [banks, setBanks] = useState<BankAccount[]>([]);
  const [payments, setPayments] = useState<BankPaymentRef[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<BankAccountInput>(EMPTY_FORM);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [list, all] = await Promise.all([ds.listBankAccounts(), ds.getAll()]);
      setBanks(list);
      setPayments(
        all.billingPeriods.flatMap((p) =>
          (p.payments ?? []).map((pay) => ({ bankAccountId: pay.bankAccountId, amount: pay.amount.amount })),
        ),
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const balances = useMemo(() => bankBalances(payments, banks), [payments, banks]);

  const resetForm = () => {
    setEditingId(null);
    setForm(EMPTY_FORM);
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (busy || !form.name.trim() || !form.accountNumber.trim()) return;
    setBusy(true);
    try {
      const ds = getPortalDataSource();
      if (editingId) {
        await ds.updateBankAccount(editingId, form);
      } else {
        await ds.createBankAccount(form);
      }
      resetForm();
      await load();
    } finally {
      setBusy(false);
    }
  };

  const beginEdit = (bank: BankAccount) => {
    setEditingId(bank.id);
    setForm({
      name: bank.name,
      accountNumber: bank.accountNumber,
      currency: bank.currency,
      gatewayMapping: bank.gatewayMapping ?? "",
    });
  };

  const toggleStatus = async (bank: BankAccount) => {
    setBusy(true);
    try {
      await getPortalDataSource().setBankAccountStatus(
        bank.id,
        bank.status === "Active" ? "Inactive" : "Active",
      );
      await load();
    } finally {
      setBusy(false);
    }
  };

  const inputClass = "w-full rounded-md border border-input bg-background px-3 py-2 text-sm";

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <CardTitle className="text-lg">{t("bankRegistryTitle" as never)}</CardTitle>
            <BetaBadge lang={lang} />
          </div>
          <CardDescription>{t("bankRegistryDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-5">
          {canManage ? (
            <form className="grid gap-3 rounded-lg border border-border bg-background p-4 sm:grid-cols-2" onSubmit={submit}>
              <div className="sm:col-span-2 text-sm font-semibold">
                {editingId ? t("bankEditTitle" as never) : t("bankAddTitle" as never)}
              </div>
              <label className="text-sm">
                <span className="mb-1 block text-muted-foreground">{t("bankFieldName" as never)}</span>
                <input
                  type="text"
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  className={inputClass}
                />
              </label>
              <label className="text-sm">
                <span className="mb-1 block text-muted-foreground">{t("bankFieldNumber" as never)}</span>
                <input
                  type="text"
                  value={form.accountNumber}
                  onChange={(e) => setForm((f) => ({ ...f, accountNumber: e.target.value }))}
                  className={inputClass}
                  dir="ltr"
                />
              </label>
              <label className="text-sm">
                <span className="mb-1 block text-muted-foreground">{t("bankFieldCurrency" as never)}</span>
                <input
                  type="text"
                  value={form.currency}
                  onChange={(e) => setForm((f) => ({ ...f, currency: e.target.value }))}
                  className={inputClass}
                  maxLength={3}
                />
              </label>
              <label className="text-sm">
                <span className="mb-1 block text-muted-foreground">{t("bankFieldGateway" as never)}</span>
                <input
                  type="text"
                  value={form.gatewayMapping ?? ""}
                  onChange={(e) => setForm((f) => ({ ...f, gatewayMapping: e.target.value }))}
                  className={inputClass}
                  placeholder={t("bankFieldGatewayHint" as never)}
                  dir="ltr"
                />
              </label>
              <div className="flex items-center gap-2 sm:col-span-2">
                <Button type="submit" size="sm" disabled={busy || !form.name.trim() || !form.accountNumber.trim()}>
                  {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />}
                  {editingId ? t("bankSave" as never) : t("bankAdd" as never)}
                </Button>
                {editingId ? (
                  <Button type="button" size="sm" variant="ghost" onClick={resetForm}>
                    <X className="h-4 w-4" />
                    {t("bankCancel" as never)}
                  </Button>
                ) : null}
              </div>
            </form>
          ) : (
            <p className="text-sm text-muted-foreground">{t("bankManageHint" as never)}</p>
          )}

          {loading ? (
            <div className="flex items-center gap-2 text-base text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-border text-muted-foreground">
                    <th className="py-2 text-start font-medium">{t("bankColCode" as never)}</th>
                    <th className="py-2 text-start font-medium">{t("bankColName" as never)}</th>
                    <th className="py-2 text-start font-medium">{t("bankColNumber" as never)}</th>
                    <th className="py-2 text-start font-medium">{t("bankColCurrency" as never)}</th>
                    <th className="py-2 text-start font-medium">{t("bankColStatus" as never)}</th>
                    {canManage ? <th className="py-2 text-end font-medium">{t("bankColActions" as never)}</th> : null}
                  </tr>
                </thead>
                <tbody>
                  {banks.map((bank) => (
                    <tr key={bank.id} className="border-b border-border/60">
                      <td className="py-2 font-mono tabular-nums">
                        {bank.code}
                        <span className="ms-2 text-xs text-muted-foreground">← {BANK_PARENT_CODE}</span>
                      </td>
                      <td className="py-2">{bank.name}</td>
                      <td className="py-2 font-mono" dir="ltr">{maskAccountNumber(bank.accountNumber)}</td>
                      <td className="py-2">{bank.currency}</td>
                      <td className="py-2">
                        <span
                          className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                            bank.status === "Active"
                              ? "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300"
                              : "bg-slate-500/15 text-slate-600 dark:text-slate-300"
                          }`}
                        >
                          {bank.status === "Active"
                            ? t("bankStatusActive" as never)
                            : t("bankStatusInactive" as never)}
                        </span>
                      </td>
                      {canManage ? (
                        <td className="py-2">
                          <div className="flex items-center justify-end gap-1.5">
                            <Button size="sm" variant="outline" onClick={() => beginEdit(bank)}>
                              <Pencil className="h-3.5 w-3.5" />
                              {t("bankEdit" as never)}
                            </Button>
                            <Button size="sm" variant="ghost" disabled={busy} onClick={() => void toggleStatus(bank)}>
                              {bank.status === "Active" ? (
                                <PowerOff className="h-3.5 w-3.5" />
                              ) : (
                                <Power className="h-3.5 w-3.5" />
                              )}
                              {bank.status === "Active"
                                ? t("bankDeactivate" as never)
                                : t("bankActivate" as never)}
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

          <p className="text-xs text-muted-foreground">{t("bankGatewaySeamNote" as never)}</p>
        </CardContent>
      </Card>

      {/* Per-bank balances rolled up to the 1100 parent (compute-only, mock). */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <CardTitle className="text-lg">{t("bankBalancesTitle" as never)}</CardTitle>
            <BetaBadge lang={lang} />
          </div>
          <CardDescription>{t("bankBalancesDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border text-muted-foreground">
                <th className="py-2 text-start font-medium">{t("bankColCode" as never)}</th>
                <th className="py-2 text-start font-medium">{t("bankColName" as never)}</th>
                <th className="py-2 text-end font-medium">{t("bankBalanceColTotal" as never)}</th>
              </tr>
            </thead>
            <tbody>
              {balances.perBank.map((row) => (
                <tr key={row.code} className="border-b border-border/60">
                  <td className="py-2 font-mono tabular-nums">{row.code}</td>
                  <td className="py-2">{row.name}</td>
                  <td className="py-2 text-end tabular-nums">
                    <MoneyAmount amount={row.total} />
                  </td>
                </tr>
              ))}
              <tr className="border-b border-border/60 text-muted-foreground">
                <td className="py-2 font-mono tabular-nums">{BANK_PARENT_CODE}</td>
                <td className="py-2">{t("bankParentDirect" as never)}</td>
                <td className="py-2 text-end tabular-nums">
                  <MoneyAmount amount={balances.parentDirect} />
                </td>
              </tr>
              <tr className="font-semibold">
                <td className="py-2 font-mono tabular-nums">{BANK_PARENT_CODE}</td>
                <td className="py-2">{t("bankRollupTotal" as never)}</td>
                <td className="py-2 text-end tabular-nums">
                  <MoneyAmount amount={balances.rollupTotal} />
                </td>
              </tr>
            </tbody>
          </table>
        </CardContent>
      </Card>
    </div>
  );
}
