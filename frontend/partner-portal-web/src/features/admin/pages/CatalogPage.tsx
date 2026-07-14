import { useCallback, useEffect, useMemo, useState } from "react";
import { Archive, Pencil, Plus, Send } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { MoneyAmount } from "@/components/MoneyAmount";
import { StatusBadge } from "@/components/catalog/badges";
import { CardGridSkeleton, ErrorState } from "@/components/states";
import { usePortalSession } from "@/features/auth/usePortalSession";
import {
  canAuthorOfferingPresentation,
  groupCatalogTiers,
  isSelfServiceOfferingKind,
  resolveCatalogAuthoringMode,
} from "@/lib/catalog/catalogAuthoring";
import { portalRoleToPriceViewer, projectCatalogPrice } from "@/lib/catalog/catalogPriceVisibility";
import { getPortalDataSource } from "@/lib/data";
import type { OfferingKind, Partner, PartnerCatalogItem } from "@/lib/data/types";
import { listUsagePackages, setPackageExplanation } from "@/lib/usage/usagePackageStore";
import type { UsagePackage } from "@/lib/usage/usagePackage";
import { useTranslator } from "@/lib/i18n";
import { offeringKindLabel, participationModeLabel, settlementBookLabel } from "@/lib/i18n/domainLabels";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { PageHeader } from "../components/PageHeader";
import { PartnerBriefEditor } from "../components/PartnerBriefEditor";
import { ProductPresentationEditor } from "../components/ProductPresentationEditor";
import { getListing, saveListing } from "@/lib/catalog/listingStore";
import { PartnerServiceOrdersPanel } from "../components/ServiceOrderPanels";
import { TableEmptyRow } from "../components/EmptyState";
import { filterByPartnerIds, useScopePartnerIds } from "../hooks/useScopePartnerIds";
import type { ModuleScopeProps } from "../moduleScope";

type CatalogPageProps = ModuleScopeProps & {
  titleKey?: string;
  descKey?: string;
};

const SERVICE_KINDS: OfferingKind[] = ["ServiceOneOff", "ServiceSubscription"];

export function CatalogPage({
  lang,
  moduleId,
  partnerId,
  titleKey = "navCatalog",
  descKey = "catalogDesc",
}: CatalogPageProps) {
  const t = useTranslator(lang);
  const session = usePortalSession();
  const scopeIds = useScopePartnerIds(moduleId, partnerId);
  const priceViewer = portalRoleToPriceViewer(session.role as never);

  const [items, setItems] = useState<PartnerCatalogItem[]>([]);
  const [partner, setPartner] = useState<Partner | undefined>();
  const [packages, setPackages] = useState<UsagePackage[]>([]);
  const [presentationItem, setPresentationItem] = useState<PartnerCatalogItem | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [formCode, setFormCode] = useState("");
  const [formName, setFormName] = useState("");
  const [formAmount, setFormAmount] = useState("49");
  const [formKind, setFormKind] = useState<OfferingKind>("ServiceOneOff");

  const showHeader = !moduleId && !partnerId;
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

  const canWrite = authoringMode === "self-service" || authoringMode === "admin-managed-write";
  const canEditBrief = canAuthorSelf || canAuthorManaged;
  const canEditPresentation = canAuthorOfferingPresentation(authoringMode);
  const tierGroups = useMemo(
    () => (authoringMode === "self-service" ? groupCatalogTiers(items) : []),
    [authoringMode, items],
  );

  const reload = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const ds = getPortalDataSource();
      const [all, partners] = await Promise.all([
        ds.getCatalogItems(partnerId),
        ds.getPartners(),
      ]);
      setItems(filterByPartnerIds(all, scopeIds));
      setPackages(partnerId ? listUsagePackages(partnerId) : []);
      if (partnerId) {
        setPartner(partners.find((p) => p.id === partnerId));
      } else {
        setPartner(undefined);
      }
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }, [partnerId, scopeIds]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const resetForm = () => {
    setEditId(null);
    setFormCode("");
    setFormName("");
    setFormAmount("49");
    setFormKind("ServiceOneOff");
    setShowForm(false);
  };

  const handleSave = async () => {
    if (!partnerId || !formCode.trim() || !formName.trim()) return;
    setBusy(true);
    try {
      const ds = getPortalDataSource();
      const partnerCost = { amount: Number(formAmount) || 0, currency: "SAR", vatInclusive: true };
      if (editId) {
        await ds.updateCatalogItem(editId, {
          name: formName,
          partnerCost,
        });
      } else {
        await ds.createCatalogItem({
          partnerId,
          code: formCode,
          name: formName,
          offeringKind: formKind,
          partnerCost,
          participationMode: partner?.participationMode,
          settlementBook: "Integration",
        });
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
      await getPortalDataSource().publishCatalogItem(id);
      await reload();
    } finally {
      setBusy(false);
    }
  };

  const handleArchive = async (id: string) => {
    setBusy(true);
    try {
      await getPortalDataSource().archiveCatalogItem(id);
      await reload();
    } finally {
      setBusy(false);
    }
  };

  const handleSaveBrief = async (brief: string | undefined) => {
    if (!partnerId) return;
    setBusy(true);
    try {
      await getPortalDataSource().updatePartnerBrief(partnerId, brief);
      await reload();
    } finally {
      setBusy(false);
    }
  };

  const handleSaveOffering = async (
    id: string,
    input: { description?: string; merchantBenefit?: string },
  ) => {
    setBusy(true);
    try {
      const updated = await getPortalDataSource().updateCatalogPresentation(id, input);
      await reload();
      setPresentationItem(updated);
    } finally {
      setBusy(false);
    }
  };

  const handleSavePackageNote = (packageId: string, note: string | undefined) => {
    setPackageExplanation(packageId, note);
    if (partnerId) setPackages(listUsagePackages(partnerId));
  };

  const startEdit = (item: PartnerCatalogItem) => {
    setEditId(item.id);
    setFormCode(item.code);
    setFormName(item.name);
    setFormAmount(String(item.partnerCost.amount));
    setFormKind(item.offeringKind);
    setShowForm(true);
  };

  const renderPriceCell = (item: PartnerCatalogItem) => {
    const activationSell = undefined;
    const scoped = projectCatalogPrice(
      { buy: item.partnerCost, sell: activationSell },
      priceViewer,
    );
    return (
      <div className="space-y-0.5 text-end tabular-nums">
        {scoped.buy ? (
          <div>
            <span className="text-xs text-muted-foreground">{t("catalogBuyLabel" as never)} </span>
            <MoneyAmount amount={scoped.buy.amount} />
          </div>
        ) : null}
        {scoped.sell ? (
          <div>
            <span className="text-xs text-muted-foreground">{t("catalogSellLabel" as never)} </span>
            <MoneyAmount amount={scoped.sell.amount} />
          </div>
        ) : null}
        {scoped.margin ? (
          <div>
            <span className="text-xs text-muted-foreground">{t("settlementMargin" as never)} </span>
            <MoneyAmount amount={scoped.margin.amount} />
          </div>
        ) : null}
      </div>
    );
  };

  const renderItemRow = (item: PartnerCatalogItem, actions?: boolean) => (
    <tr key={item.id} className="border-b border-border/60">
      <td className="px-2 py-2">
        <button
          type="button"
          className="text-start font-medium text-foreground underline-offset-2 hover:underline"
          onClick={() => setPresentationItem(item)}
        >
          {item.name}
        </button>
        <span className="ms-2 text-xs text-muted-foreground">{item.code}</span>
        {item.merchantBenefit ? (
          <p className="mt-0.5 text-xs text-teal-700 dark:text-teal-300">{item.merchantBenefit}</p>
        ) : null}
        {item.description ? (
          <p className="mt-0.5 text-xs text-muted-foreground">{item.description}</p>
        ) : null}
      </td>
      <td className="px-2 py-2">{offeringKindLabel(lang, item.offeringKind)}</td>
      <td className="px-2 py-2">{participationModeLabel(lang, item.participationMode)}</td>
      <td className="px-2 py-2">{settlementBookLabel(lang, item.settlementBook)}</td>
      <td className="px-2 py-2">
        <StatusBadge status={item.status} label={item.status} />
      </td>
      <td className="px-2 py-2">{renderPriceCell(item)}</td>
      {actions ? (
        <td className="px-2 py-2 text-end">
          <div className="flex flex-wrap justify-end gap-1">
            {item.status === "Draft" ? (
              <>
                <Button size="sm" variant="ghost" disabled={busy} onClick={() => startEdit(item)}>
                  <Pencil className="h-3.5 w-3.5" />
                </Button>
                <Button size="sm" variant="outline" disabled={busy} onClick={() => void handlePublish(item.id)}>
                  <Send className="h-3.5 w-3.5" />
                  {t("catalogPublish" as never)}
                </Button>
              </>
            ) : null}
            {item.status !== "Archived" ? (
              <Button size="sm" variant="ghost" disabled={busy} onClick={() => void handleArchive(item.id)}>
                <Archive className="h-3.5 w-3.5" />
              </Button>
            ) : null}
          </div>
        </td>
      ) : null}
    </tr>
  );

  const readOnlyNotice =
    authoringMode === "admin-managed-readonly" ? (
      <p className="rounded-md border border-border bg-muted/40 px-3 py-2 text-sm text-muted-foreground">
        {t("catalogPurchaseAgreementReadOnly" as never)}
      </p>
    ) : null;

  if (presentationItem) {
    return (
      <ProductPresentationEditor
        key={presentationItem.id}
        lang={lang}
        item={presentationItem}
        packages={packages}
        canEdit={canEditPresentation}
        busy={busy}
        onBack={() => setPresentationItem(null)}
        onSaveOffering={(input) => void handleSaveOffering(presentationItem.id, input)}
        onSavePackageNote={handleSavePackageNote}
        listing={getListing(presentationItem.id)}
        onSaveListing={(next) => {
          saveListing(next);
        }}
      />
    );
  }

  return (
    <div className="space-y-6">
      {showHeader ? (
        <PageHeader title={t(titleKey as never)} description={t(descKey as never)} lang={lang} />
      ) : null}

      {readOnlyNotice}

      {partnerId ? (
        <PartnerBriefEditor
          key={partner?.id ?? partnerId}
          lang={lang}
          brief={partner?.partnerBrief}
          canEdit={canEditBrief}
          busy={busy}
          onSave={(brief) => void handleSaveBrief(brief)}
        />
      ) : null}

      {partnerId ? <PartnerServiceOrdersPanel lang={lang} partnerId={partnerId} /> : null}

      {canWrite && partnerId ? (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between gap-4">
            <div>
              <CardTitle>{t("catalogManageTitle" as never)}</CardTitle>
              <CardDescription>{t("catalogManageDesc" as never)}</CardDescription>
            </div>
            {!showForm ? (
              <Button size="sm" onClick={() => setShowForm(true)} disabled={busy}>
                <Plus className="h-4 w-4" />
                {t("catalogAddTier" as never)}
              </Button>
            ) : null}
          </CardHeader>
          {showForm ? (
            <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {!editId ? (
                <>
                  <div className="space-y-1">
                    <Label htmlFor="cat-code">{t("catalogFormCode" as never)}</Label>
                    <Input id="cat-code" value={formCode} onChange={(e) => setFormCode(e.target.value)} />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="cat-kind">{t("colOffering" as never)}</Label>
                    <select
                      id="cat-kind"
                      className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                      value={formKind}
                      onChange={(e) => setFormKind(e.target.value as OfferingKind)}
                    >
                      {SERVICE_KINDS.filter((k) =>
                        authoringMode === "admin-managed-write" ? true : isSelfServiceOfferingKind(k),
                      ).map((k) => (
                        <option key={k} value={k}>
                          {offeringKindLabel(lang, k)}
                        </option>
                      ))}
                    </select>
                  </div>
                </>
              ) : null}
              <div className="space-y-1">
                <Label htmlFor="cat-name">{t("colItem" as never)}</Label>
                <Input id="cat-name" value={formName} onChange={(e) => setFormName(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="cat-amount">{t("catalogBuyLabel" as never)}</Label>
                <Input
                  id="cat-amount"
                  type="number"
                  min={0}
                  step="0.01"
                  value={formAmount}
                  onChange={(e) => setFormAmount(e.target.value)}
                />
              </div>
              <div className="flex items-end gap-2 sm:col-span-2 lg:col-span-4">
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
          <CardTitle>
            {authoringMode === "self-service" && tierGroups.length > 0
              ? t("catalogTiersTitle" as never)
              : t("catalogAllTitle" as never)}
          </CardTitle>
          <CardDescription>{t("mockSampleNotice" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <CardGridSkeleton count={2} />
          ) : loadError ? (
            <ErrorState
              message={t("stateErrorGeneric" as never)}
              retryLabel={t("stateRetry" as never)}
              onRetry={() => void reload()}
            />
          ) : tierGroups.length > 0 ? (
            <div className="space-y-8">
              {tierGroups.map((group) => (
                <div key={group.groupKey}>
                  <h3 className="mb-2 text-sm font-semibold text-foreground">{group.label}</h3>
                  <div className="overflow-x-auto">
                    <table className="w-full min-w-[720px] text-sm">
                      <thead>
                        <tr className="border-b border-border text-start text-muted-foreground">
                          <th className="px-2 py-2 font-medium">{t("colItem" as never)}</th>
                          <th className="px-2 py-2 font-medium">{t("colOffering" as never)}</th>
                          <th className="px-2 py-2 font-medium">{t("colMode" as never)}</th>
                          <th className="px-2 py-2 font-medium">{t("colBook" as never)}</th>
                          <th className="px-2 py-2 font-medium">{t("colStatus" as never)}</th>
                          <th className="px-2 py-2 text-end font-medium">{t("colPrice" as never)}</th>
                          {canWrite ? <th className="px-2 py-2 text-end font-medium">{t("colActions" as never)}</th> : null}
                        </tr>
                      </thead>
                      <tbody>
                        {group.items.map((item) => renderItemRow(item, canWrite))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px] text-sm">
                <thead>
                  <tr className="border-b border-border text-start text-muted-foreground">
                    <th className="px-2 py-2 font-medium">{t("colItem" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colOffering" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colMode" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colBook" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colStatus" as never)}</th>
                    <th className="px-2 py-2 text-end font-medium">{t("colPrice" as never)}</th>
                    {canWrite ? <th className="px-2 py-2 text-end font-medium">{t("colActions" as never)}</th> : null}
                  </tr>
                </thead>
                <tbody>
                  {items.length === 0 ? (
                    <TableEmptyRow colSpan={canWrite ? 7 : 6} message={t("catalogEmpty" as never)} />
                  ) : null}
                  {items.map((item) => renderItemRow(item, canWrite))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
