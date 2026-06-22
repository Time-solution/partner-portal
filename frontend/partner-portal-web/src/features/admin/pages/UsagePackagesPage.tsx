import { useCallback, useEffect, useMemo, useState } from "react";
import { Archive, Loader2, Pencil, Plus, Send } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { MoneyAmount } from "@/components/MoneyAmount";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { resolveCatalogAuthoringMode } from "@/lib/catalog/catalogAuthoring";
import { portalRoleToPriceViewer } from "@/lib/catalog/catalogPriceVisibility";
import { getPortalDataSource } from "@/lib/data";
import type { Partner } from "@/lib/data/types";
import { useTranslator } from "@/lib/i18n";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import {
  scopeUsagePackage,
  type ScopedPackageLeg,
  type UsagePackage,
  type UsagePackageMode,
  type UsagePackageTier,
} from "@/lib/usage/usagePackage";
import {
  archiveUsagePackage,
  createUsagePackage,
  listUsagePackages,
  publishUsagePackage,
  updateUsagePackage,
} from "@/lib/usage/usagePackageStore";
import { PageHeader } from "../components/PageHeader";
import { TableEmptyRow } from "../components/EmptyState";
import type { ModuleScopeProps } from "../moduleScope";

type UsagePackagesPageProps = ModuleScopeProps & {
  titleKey?: string;
  descKey?: string;
};

function statusBadgeClass(status: UsagePackage["status"]): string {
  switch (status) {
    case "Published":
      return "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300";
    case "Draft":
      return "bg-amber-500/15 text-amber-700 dark:text-amber-300";
    default:
      return "bg-muted text-muted-foreground";
  }
}

interface TierRow {
  fromQuantity: string;
  toQuantity: string; // "" = open-ended
  buyRate: string;
  sellRate: string;
}

interface FormState {
  name: string;
  mode: UsagePackageMode;
  unitLabel: string;
  includedQuantity: string;
  baseBuyAmount: string;
  baseSellAmount: string;
  overageBuyAmount: string;
  overageSellAmount: string;
  payer: "Merchant" | "Partner";
  tiers: TierRow[];
}

const EMPTY_FORM: FormState = {
  name: "",
  mode: "Resale",
  unitLabel: "messages",
  includedQuantity: "0",
  baseBuyAmount: "0",
  baseSellAmount: "0",
  overageBuyAmount: "0",
  overageSellAmount: "0",
  payer: "Merchant",
  tiers: [],
};

export function UsagePackagesPage({
  lang,
  partnerId,
  titleKey = "usagePkgTitle",
  descKey = "usagePkgDesc",
}: UsagePackagesPageProps) {
  const t = useTranslator(lang);
  const session = usePortalSession();
  const priceViewer = portalRoleToPriceViewer(session.role as never);

  const [packages, setPackages] = useState<UsagePackage[]>([]);
  const [partner, setPartner] = useState<Partner | undefined>();
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(EMPTY_FORM);

  const showHeader = !partnerId;
  const canAuthorSelf = session.can(PortalPermissions.Catalog.AuthorSelf);
  const canAuthorManaged = session.can(PortalPermissions.Catalog.AuthorManaged);

  const authoringMode = useMemo(
    () =>
      resolveCatalogAuthoringMode({
        partnerType: partner?.type ?? "Service",
        canAuthorSelf,
        canAuthorManaged,
      }),
    [partner?.type, canAuthorSelf, canAuthorManaged],
  );
  const canWrite =
    Boolean(partnerId) && (authoringMode === "self-service" || authoringMode === "admin-managed-write");

  const reload = useCallback(async () => {
    setLoading(true);
    if (partnerId) {
      const partners = await getPortalDataSource().getPartners();
      setPartner(partners.find((p) => p.id === partnerId));
    } else {
      setPartner(undefined);
    }
    setPackages(listUsagePackages(partnerId));
    setLoading(false);
  }, [partnerId]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const resetForm = () => {
    setEditId(null);
    setForm(EMPTY_FORM);
    setShowForm(false);
  };

  const handleSave = async () => {
    if (!partnerId || !form.name.trim()) return;
    setBusy(true);
    try {
      const payload = {
        partnerId,
        name: form.name,
        unitLabel: form.unitLabel,
        mode: form.mode,
        currency: "SAR",
        includedQuantity: Number(form.includedQuantity) || 0,
        baseBuyAmount: Number(form.baseBuyAmount) || 0,
        baseSellAmount: Number(form.baseSellAmount) || 0,
        overageBuyAmount: Number(form.overageBuyAmount) || 0,
        overageSellAmount: Number(form.overageSellAmount) || 0,
        payer: form.payer,
        tiers: form.tiers.map<UsagePackageTier>((row) => ({
          fromQuantity: Number(row.fromQuantity) || 0,
          toQuantity: row.toQuantity.trim() === "" ? null : Number(row.toQuantity),
          buyRate: Number(row.buyRate) || 0,
          sellRate: Number(row.sellRate) || 0,
        })),
      };
      if (editId) {
        updateUsagePackage(editId, payload);
      } else {
        createUsagePackage(payload);
      }
      resetForm();
      await reload();
    } finally {
      setBusy(false);
    }
  };

  const handlePublish = async (id: string) => {
    setBusy(true);
    try {
      publishUsagePackage(id);
      await reload();
    } finally {
      setBusy(false);
    }
  };

  const handleArchive = async (id: string) => {
    setBusy(true);
    try {
      archiveUsagePackage(id);
      await reload();
    } finally {
      setBusy(false);
    }
  };

  const startEdit = (pkg: UsagePackage) => {
    setEditId(pkg.id);
    setForm({
      name: pkg.name,
      mode: pkg.mode,
      unitLabel: pkg.unitLabel,
      includedQuantity: String(pkg.includedQuantity),
      baseBuyAmount: String(pkg.baseBuyAmount),
      baseSellAmount: String(pkg.baseSellAmount),
      overageBuyAmount: String(pkg.overageBuyAmount),
      overageSellAmount: String(pkg.overageSellAmount),
      payer: pkg.payer,
      tiers: (pkg.tiers ?? []).map((tier) => ({
        fromQuantity: String(tier.fromQuantity),
        toQuantity: tier.toQuantity == null ? "" : String(tier.toQuantity),
        buyRate: String(tier.buyRate),
        sellRate: String(tier.sellRate),
      })),
    });
    setShowForm(true);
  };

  const updateTier = (index: number, patch: Partial<TierRow>) =>
    setForm((f) => ({
      ...f,
      tiers: f.tiers.map((row, i) => (i === index ? { ...row, ...patch } : row)),
    }));

  const addTier = () =>
    setForm((f) => {
      const last = f.tiers[f.tiers.length - 1];
      const from = last ? (last.toQuantity.trim() === "" ? "" : last.toQuantity) : "0";
      return { ...f, tiers: [...f.tiers, { fromQuantity: from, toQuantity: "", buyRate: "0", sellRate: "0" }] };
    });

  const removeTier = (index: number) =>
    setForm((f) => ({ ...f, tiers: f.tiers.filter((_, i) => i !== index) }));

  const renderLeg = (leg: ScopedPackageLeg, mode: UsagePackageMode) => (
    <div className="space-y-0.5 text-end tabular-nums">
      {mode === "Subscription" && leg.fee ? (
        <div>
          <span className="text-xs text-muted-foreground">{t("usagePkgFee" as never)} </span>
          <MoneyAmount amount={leg.fee.amount} />
        </div>
      ) : null}
      {leg.buy ? (
        <div>
          <span className="text-xs text-muted-foreground">{t("usagePkgBaseBuy" as never)} </span>
          <MoneyAmount amount={leg.buy.amount} />
        </div>
      ) : null}
      {leg.sell ? (
        <div>
          <span className="text-xs text-muted-foreground">{t("usagePkgBaseSell" as never)} </span>
          <MoneyAmount amount={leg.sell.amount} />
        </div>
      ) : null}
      {leg.margin ? (
        <div>
          <span className="text-xs text-muted-foreground">{t("usagePkgMargin" as never)} </span>
          <MoneyAmount amount={leg.margin.amount} />
        </div>
      ) : null}
    </div>
  );

  const renderRow = (pkg: UsagePackage) => {
    const scoped = scopeUsagePackage(pkg, priceViewer);
    return (
      <tr key={pkg.id} className="border-b border-border/60 align-top">
        <td className="px-2 py-2">
          <span className="font-medium">{pkg.name}</span>
          <span className="ms-2 text-xs text-muted-foreground">{pkg.unitLabel}</span>
        </td>
        <td className="px-2 py-2">
          {pkg.mode === "Resale" ? t("usagePkgModeResale" as never) : t("usagePkgModeSubscription" as never)}
        </td>
        <td className="px-2 py-2 tabular-nums">{pkg.includedQuantity.toLocaleString()}</td>
        <td className="px-2 py-2">{renderLeg(scoped.base, pkg.mode)}</td>
        <td className="px-2 py-2">{renderLeg(scoped.overage, pkg.mode)}</td>
        <td className="px-2 py-2">
          {scoped.payer
            ? scoped.payer === "Merchant"
              ? t("usagePkgPayerMerchant" as never)
              : t("usagePkgPayerPartner" as never)
            : "—"}
        </td>
        <td className="px-2 py-2">
          <span className={`inline-flex rounded-md border px-2 py-0.5 text-xs font-medium ${statusBadgeClass(pkg.status)}`}>
            {pkg.status}
          </span>
        </td>
        {canWrite ? (
          <td className="px-2 py-2 text-end">
            <div className="flex flex-wrap justify-end gap-1">
              {pkg.status === "Draft" ? (
                <>
                  <Button size="sm" variant="ghost" disabled={busy} onClick={() => startEdit(pkg)}>
                    <Pencil className="h-3.5 w-3.5" />
                  </Button>
                  <Button size="sm" variant="outline" disabled={busy} onClick={() => void handlePublish(pkg.id)}>
                    <Send className="h-3.5 w-3.5" />
                    {t("usagePkgPublish" as never)}
                  </Button>
                </>
              ) : null}
              {pkg.status !== "Archived" ? (
                <Button size="sm" variant="ghost" disabled={busy} onClick={() => void handleArchive(pkg.id)}>
                  <Archive className="h-3.5 w-3.5" />
                </Button>
              ) : null}
            </div>
          </td>
        ) : null}
      </tr>
    );
  };

  const readOnlyNotice =
    Boolean(partnerId) && authoringMode === "admin-managed-readonly" ? (
      <p className="rounded-md border border-border bg-muted/40 px-3 py-2 text-sm text-muted-foreground">
        {t("usagePkgReadOnly" as never)}
      </p>
    ) : null;

  const isSubscription = form.mode === "Subscription";

  return (
    <div className="space-y-6">
      {showHeader ? (
        <PageHeader title={t(titleKey as never)} description={t(descKey as never)} lang={lang} />
      ) : null}

      {readOnlyNotice}

      {canWrite ? (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between gap-4">
            <div>
              <CardTitle>{t("usagePkgManageTitle" as never)}</CardTitle>
              <CardDescription>{t("usagePkgManageDesc" as never)}</CardDescription>
            </div>
            {!showForm ? (
              <Button size="sm" onClick={() => setShowForm(true)} disabled={busy}>
                <Plus className="h-4 w-4" />
                {t("usagePkgAdd" as never)}
              </Button>
            ) : null}
          </CardHeader>
          {showForm ? (
            <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <div className="space-y-1">
                <Label htmlFor="pkg-name">{t("usagePkgName" as never)}</Label>
                <Input id="pkg-name" value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="pkg-mode">{t("usagePkgMode" as never)}</Label>
                <select
                  id="pkg-mode"
                  className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                  value={form.mode}
                  disabled={Boolean(editId)}
                  onChange={(e) => setForm((f) => ({ ...f, mode: e.target.value as UsagePackageMode }))}
                >
                  <option value="Resale">{t("usagePkgModeResale" as never)}</option>
                  <option value="Subscription">{t("usagePkgModeSubscription" as never)}</option>
                </select>
              </div>
              <div className="space-y-1">
                <Label htmlFor="pkg-unit">{t("usagePkgUnit" as never)}</Label>
                <Input id="pkg-unit" value={form.unitLabel} onChange={(e) => setForm((f) => ({ ...f, unitLabel: e.target.value }))} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="pkg-included">{t("usagePkgIncluded" as never)}</Label>
                <Input id="pkg-included" type="number" min={0} step="1" value={form.includedQuantity} onChange={(e) => setForm((f) => ({ ...f, includedQuantity: e.target.value }))} />
              </div>
              {!isSubscription ? (
                <div className="space-y-1">
                  <Label htmlFor="pkg-base-buy">{t("usagePkgBaseBuy" as never)}</Label>
                  <Input id="pkg-base-buy" type="number" min={0} step="0.01" value={form.baseBuyAmount} onChange={(e) => setForm((f) => ({ ...f, baseBuyAmount: e.target.value }))} />
                </div>
              ) : null}
              <div className="space-y-1">
                <Label htmlFor="pkg-base-sell">{isSubscription ? t("usagePkgFee" as never) : t("usagePkgBaseSell" as never)}</Label>
                <Input id="pkg-base-sell" type="number" min={0} step="0.01" value={form.baseSellAmount} onChange={(e) => setForm((f) => ({ ...f, baseSellAmount: e.target.value }))} />
              </div>
              {!isSubscription ? (
                <div className="space-y-1">
                  <Label htmlFor="pkg-over-buy">{t("usagePkgOverageBuy" as never)}</Label>
                  <Input id="pkg-over-buy" type="number" min={0} step="0.01" value={form.overageBuyAmount} onChange={(e) => setForm((f) => ({ ...f, overageBuyAmount: e.target.value }))} />
                </div>
              ) : null}
              <div className="space-y-1">
                <Label htmlFor="pkg-over-sell">{t("usagePkgOverageSell" as never)}</Label>
                <Input id="pkg-over-sell" type="number" min={0} step="0.01" value={form.overageSellAmount} onChange={(e) => setForm((f) => ({ ...f, overageSellAmount: e.target.value }))} />
              </div>
              {isSubscription ? (
                <div className="space-y-1">
                  <Label htmlFor="pkg-payer">{t("usagePkgPayer" as never)}</Label>
                  <select
                    id="pkg-payer"
                    className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                    value={form.payer}
                    onChange={(e) => setForm((f) => ({ ...f, payer: e.target.value as "Merchant" | "Partner" }))}
                  >
                    <option value="Merchant">{t("usagePkgPayerMerchant" as never)}</option>
                    <option value="Partner">{t("usagePkgPayerPartner" as never)}</option>
                  </select>
                </div>
              ) : null}
              <div className="space-y-2 rounded-md border border-border/60 p-3 sm:col-span-2 lg:col-span-3">
                <div className="flex items-center justify-between gap-2">
                  <div>
                    <Label>{t("usagePkgTiers" as never)}</Label>
                    <p className="text-xs text-muted-foreground">{t("usagePkgTiersDesc" as never)}</p>
                  </div>
                  <Button size="sm" variant="outline" disabled={busy} onClick={addTier}>
                    <Plus className="h-3.5 w-3.5" />
                    {t("usagePkgTierAdd" as never)}
                  </Button>
                </div>
                {form.tiers.map((row, i) => (
                  <div key={i} className="flex flex-wrap items-end gap-2">
                    <div className="space-y-1">
                      <Label className="text-xs">{t("usagePkgTierFrom" as never)}</Label>
                      <Input
                        type="number"
                        min={0}
                        step="1"
                        className="w-24"
                        value={row.fromQuantity}
                        onChange={(e) => updateTier(i, { fromQuantity: e.target.value })}
                      />
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs">{t("usagePkgTierTo" as never)}</Label>
                      <Input
                        type="number"
                        min={0}
                        step="1"
                        className="w-24"
                        placeholder={t("usagePkgTierToOpen" as never)}
                        value={row.toQuantity}
                        onChange={(e) => updateTier(i, { toQuantity: e.target.value })}
                      />
                    </div>
                    {!isSubscription ? (
                      <div className="space-y-1">
                        <Label className="text-xs">{t("usagePkgTierBuy" as never)}</Label>
                        <Input
                          type="number"
                          min={0}
                          step="0.01"
                          className="w-24"
                          value={row.buyRate}
                          onChange={(e) => updateTier(i, { buyRate: e.target.value })}
                        />
                      </div>
                    ) : null}
                    <div className="space-y-1">
                      <Label className="text-xs">{isSubscription ? t("usagePkgFee" as never) : t("usagePkgTierSell" as never)}</Label>
                      <Input
                        type="number"
                        min={0}
                        step="0.01"
                        className="w-24"
                        value={row.sellRate}
                        onChange={(e) => updateTier(i, { sellRate: e.target.value })}
                      />
                    </div>
                    <Button size="sm" variant="ghost" disabled={busy} onClick={() => removeTier(i)}>
                      {t("usagePkgTierRemove" as never)}
                    </Button>
                  </div>
                ))}
              </div>

              <div className="flex items-end gap-2 sm:col-span-2 lg:col-span-3">
                <Button disabled={busy} onClick={() => void handleSave()}>
                  {t("catalogSave" as never)}
                </Button>
                <Button variant="ghost" disabled={busy} onClick={resetForm}>
                  {t("cancel" as never)}
                </Button>
              </div>
            </CardContent>
          ) : null}
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>{t("usagePkgTitle" as never)}</CardTitle>
          <CardDescription>{t("mockSampleNotice" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[820px] text-sm">
                <thead>
                  <tr className="border-b border-border text-start text-muted-foreground">
                    <th className="px-2 py-2 font-medium">{t("usagePkgName" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("usagePkgMode" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("usagePkgIncluded" as never)}</th>
                    <th className="px-2 py-2 text-end font-medium">{t("usagePkgBase" as never)}</th>
                    <th className="px-2 py-2 text-end font-medium">{t("usagePkgOverage" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("usagePkgPayer" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colStatus" as never)}</th>
                    {canWrite ? <th className="px-2 py-2 text-end font-medium">{t("colActions" as never)}</th> : null}
                  </tr>
                </thead>
                <tbody>
                  {packages.length === 0 ? (
                    <TableEmptyRow colSpan={canWrite ? 8 : 7} message={t("usagePkgEmpty" as never)} />
                  ) : null}
                  {packages.map(renderRow)}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
