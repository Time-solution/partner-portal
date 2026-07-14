import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { ArrowRight, PackageCheck, ShoppingBag, Sparkles } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { EmptyState, ErrorState, CardGridSkeleton } from "@/components/states";
import { MoneyAmount } from "@/components/MoneyAmount";
import { MerchantReflectionOrders } from "@/features/merchant/MerchantReflectionOrders";
import {
  browseCategoryLabelKey,
  buildMerchantBrowseGroups,
  toMerchantViewEntry,
  type MerchantBrowsePartnerViewEntry,
  type MerchantOfferingView,
} from "@/lib/catalog/merchantBrowse";
import { getPortalDataSource } from "@/lib/data";
import { listUsagePackages } from "@/lib/usage/usagePackageStore";
import { merchantPackageDisplay, type MerchantPackageDisplay } from "@/lib/usage/usagePackageDisplay";
import {
  OfferingDetailSheet,
  PartnerBrowseCard,
} from "../components/MerchantBrowseCards";
import { getListing } from "@/lib/catalog/listingStore";
import { MerchantServiceOrdersPanel, ServiceOrderForm } from "../components/ServiceOrderPanels";
import { listOrdersForTenant } from "@/lib/orders/serviceOrders";
import { subscribePortalDataChanged } from "@/lib/data/portalDataEvents";
import { MOCK_MERCHANT_PREVIEW } from "@/lib/mock/merchantPreview";
import { OrgProfileForm } from "@/features/settings/profile/OrgProfileForm";
import { PageHeader } from "../components/PageHeader";
import { MerchantActivePartnersPanel } from "../components/MerchantActivePartnersPanel";
import { MerchantUsagePackagesPanel } from "../components/MerchantUsagePackagesPanel";
import { MerchantStatementView } from "@/features/merchant/MerchantStatementView";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { roleExperience } from "@/lib/rbac/roleNavConfig";
import type { PortalRole } from "@/lib/rbac/portalRoles";
import { tabBarClass, tabLinkClass } from "@/lib/ui/tabs";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { offeringKindLabel } from "@/lib/i18n/domainLabels";

import { parseMerchantTab, type MerchantTab } from "@/lib/rbac/merchantTabs";

export { parseMerchantTab, type MerchantTab } from "@/lib/rbac/merchantTabs";

export function MerchantPreviewPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = parseMerchantTab(searchParams.get("tab"));
  const { role } = usePortalSession();
  const isMerchantExperience = roleExperience(role as PortalRole) === "merchant";

  const [viewGroups, setViewGroups] = useState<
    { moduleId: string; partners: MerchantBrowsePartnerViewEntry[] }[]
  >([]);
  const [packagesByPartner, setPackagesByPartner] = useState<Map<string, MerchantPackageDisplay[]>>(
    new Map(),
  );
  const [activeCatalogIds, setActiveCatalogIds] = useState<Set<string>>(new Set());
  const [activeServiceCount, setActiveServiceCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [busyCatalogId, setBusyCatalogId] = useState<string | null>(null);
  const [orderItem, setOrderItem] = useState<MerchantOfferingView | null>(null);
  const [ordersVersion, setOrdersVersion] = useState(0);
  const [toast, setToast] = useState<{ message: string; showServicesAction?: boolean } | null>(null);
  const [detail, setDetail] = useState<
    { entry: MerchantBrowsePartnerViewEntry; offering: MerchantOfferingView } | null
  >(null);
  const [confirmActivate, setConfirmActivate] = useState<
    { entry: MerchantBrowsePartnerViewEntry; offering: MerchantOfferingView } | null
  >(null);
  const [confirmEndId, setConfirmEndId] = useState<string | null>(null);
  const [kindFilter, setKindFilter] = useState<string>("all");
  const [partnerFilter, setPartnerFilter] = useState<string>("all");

  const loadBrowse = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const ds = getPortalDataSource();
      const [partners, catalog, activations] = await Promise.all([
        ds.getPartners(),
        ds.getCatalogItems(),
        ds.getActivations(),
      ]);
      // Merchant scope boundary: raw items projected ONCE through the single merchant mapper.
      setViewGroups(
        buildMerchantBrowseGroups(partners, catalog).map((group) => ({
          moduleId: group.moduleId,
          partners: group.partners.map(toMerchantViewEntry),
        })),
      );
      const pkgMap = new Map<string, MerchantPackageDisplay[]>();
      for (const pkg of listUsagePackages().filter((p) => p.status === "Published")) {
        pkgMap.set(pkg.partnerId, [...(pkgMap.get(pkg.partnerId) ?? []), merchantPackageDisplay(pkg)]);
      }
      setPackagesByPartner(pkgMap);
      const ids = new Set<string>();
      let activeCount = 0;
      for (const row of activations) {
        if (
          row.activation.tenantId === MOCK_MERCHANT_PREVIEW.tenantId &&
          row.activation.status !== "Ended"
        ) {
          ids.add(row.activation.catalogItemId);
          activeCount += 1;
        }
      }
      setActiveCatalogIds(ids);
      setActiveServiceCount(activeCount);
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadBrowse();
    return subscribePortalDataChanged(() => {
      void loadBrowse();
    });
  }, [loadBrowse]);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 6000);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const setTab = (tab: MerchantTab) => {
    if (tab === "dashboard") {
      setSearchParams({});
    } else {
      setSearchParams({ tab });
    }
  };

  const openOrderCount = useMemo(
    () =>
      listOrdersForTenant(MOCK_MERCHANT_PREVIEW.tenantId).filter(
        (o) => o.status !== "Closed" && o.status !== "MerchantCancelled" && o.status !== "PartnerDeclined",
      ).length,
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [ordersVersion, activeTab],
  );

  const kindOptions = useMemo(() => {
    const kinds = new Set<string>();
    for (const group of viewGroups)
      for (const entry of group.partners)
        for (const offering of entry.offerings) kinds.add(offering.offeringKind);
    return [...kinds];
  }, [viewGroups]);

  const partnerOptions = useMemo(
    () =>
      viewGroups.flatMap((g) =>
        g.partners.map((p) => ({
          id: p.partner.id,
          name: p.partner.tradeName ?? p.partner.legalName,
        })),
      ),
    [viewGroups],
  );

  const filteredGroups = useMemo(
    () =>
      viewGroups
        .map((group) => ({
          moduleId: group.moduleId,
          partners: group.partners
            .filter((entry) => partnerFilter === "all" || entry.partner.id === partnerFilter)
            .map((entry) => ({
              ...entry,
              offerings:
                kindFilter === "all"
                  ? entry.offerings
                  : entry.offerings.filter((o) => o.offeringKind === kindFilter),
            }))
            .filter((entry) => entry.offerings.length > 0),
        }))
        .filter((group) => group.partners.length > 0),
    [viewGroups, kindFilter, partnerFilter],
  );

  const handleActivate = async (entry: MerchantBrowsePartnerViewEntry, offering: MerchantOfferingView) => {
    setBusyCatalogId(offering.id);
    try {
      await getPortalDataSource().createActivation({
        partnerId: entry.partner.id,
        catalogItemId: offering.id,
        tenantId: MOCK_MERCHANT_PREVIEW.tenantId,
        merchantName: MOCK_MERCHANT_PREVIEW.merchantName,
        resalePrice: offering.price,
      });
      setToast({ message: t("merchantBrowseActivatedToast" as never), showServicesAction: true });
      setConfirmActivate(null);
      setDetail(null);
      await loadBrowse();
    } catch (err) {
      setToast({ message: err instanceof Error ? err.message : String(err) });
      setConfirmActivate(null);
    } finally {
      setBusyCatalogId(null);
    }
  };

  const handleEnd = async (activationId: string) => {
    setBusyCatalogId(activationId);
    try {
      await getPortalDataSource().endActivation(activationId);
      setConfirmEndId(null);
      await loadBrowse();
    } finally {
      setBusyCatalogId(null);
    }
  };

  const toastBanner = toast ? (
    <div
      role="alert"
      className="flex items-start gap-3 rounded-lg border border-teal-500/40 bg-teal-500/10 px-4 py-3 text-sm text-teal-950 dark:text-teal-50"
    >
      <Sparkles className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      <span className="flex-1">{toast.message}</span>
      {toast.showServicesAction ? (
        <Button
          size="sm"
          variant="outline"
          data-testid="toast-view-services"
          onClick={() => {
            setToast(null);
            setTab("services");
          }}
        >
          {t("toastViewServices" as never)}
          <ArrowRight className="h-3.5 w-3.5 rtl:rotate-180" aria-hidden="true" />
        </Button>
      ) : null}
    </div>
  ) : null;

  const tabs: { key: MerchantTab; labelKey: string }[] = [
    { key: "dashboard", labelKey: "navMerchantDashboard" },
    { key: "browse", labelKey: "navMerchantBrowse" },
    { key: "services", labelKey: "navMerchantServices" },
    { key: "orders", labelKey: "navMerchantOrders" },
    { key: "invoices", labelKey: "navMerchantInvoices" },
    { key: "statement", labelKey: "navMerchantStatement" },
    { key: "profile", labelKey: "navMerchantProfile" },
  ];

  return (
    <div className="space-y-6" dir={isRtl ? "rtl" : "ltr"}>
      <PageHeader
        title={t("merchantPreviewTitle" as never)}
        description={t("merchantPreviewDesc" as never)}
        lang={lang}
      />

      <div
        role="status"
        className="rounded-lg border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm text-amber-950 dark:text-amber-100"
      >
        <p className="font-medium">{t("merchantPreviewBannerTitle" as never)}</p>
        <p className="mt-1 text-amber-900/90 dark:text-amber-100/90">
          {t("merchantPreviewBannerDesc" as never)}
        </p>
      </div>

      {/* The merchant role drives these views from the SIDEBAR; the in-page tab bar stays for admin preview. */}
      {!isMerchantExperience ? (
        <nav className={tabBarClass} aria-label={t("merchantPreviewNav" as never)}>
          {tabs.map((tab) => (
            <button
              key={tab.key}
              type="button"
              className={tabLinkClass(activeTab === tab.key)}
              onClick={() => setTab(tab.key)}
            >
              {t(tab.labelKey as never)}
            </button>
          ))}
        </nav>
      ) : null}

      {toastBanner}

      {activeTab === "profile" ? (
        <OrgProfileForm
          lang={lang}
          scope={{ kind: "merchant", id: MOCK_MERCHANT_PREVIEW.tenantId }}
          requireNationalNumber
          titleKey="orgProfileMerchantTitle"
          descKey="orgProfileMerchantDesc"
        />
      ) : activeTab === "statement" ? (
        <MerchantStatementView lang={lang} tenantId={MOCK_MERCHANT_PREVIEW.tenantId} />
      ) : activeTab === "services" ? (
        <div className="space-y-4" data-testid="merchant-services-view">
          <MerchantActivePartnersPanel
            lang={lang}
            tenantId={MOCK_MERCHANT_PREVIEW.tenantId}
            onDeactivate={(id) => setConfirmEndId(id)}
            busyActivationId={busyCatalogId}
            onInvoicesLink={() => setTab("invoices")}
          />
        </div>
      ) : activeTab === "orders" ? (
        <div className="space-y-4" data-testid="merchant-orders-view">
          {orderItem ? (
            <ServiceOrderForm
              lang={lang}
              catalogItemId={orderItem.id}
              offeringName={orderItem.name}
              partnerId={orderItem.partnerId}
              tenantId={MOCK_MERCHANT_PREVIEW.tenantId}
              participationMode={
                orderItem.participationMode === "SubscriptionFee" ? "SubscriptionFee" : "Principal"
              }
              sellOrFee={orderItem.price.amount}
              requirements={getListing(orderItem.id).requirements}
              onDone={() => {
                setOrderItem(null);
                setOrdersVersion((v) => v + 1);
              }}
              onCancel={() => setOrderItem(null)}
            />
          ) : null}
          <MerchantServiceOrdersPanel
            key={ordersVersion}
            lang={lang}
            tenantId={MOCK_MERCHANT_PREVIEW.tenantId}
          />
        </div>
      ) : activeTab === "invoices" ? (
        <div className="space-y-6" data-testid="merchant-invoices-view">
          <div>
            <h2 className="text-lg font-semibold">{t("merchantInvoicesTitle" as never)}</h2>
            <p className="text-sm text-muted-foreground">{t("merchantInvoicesDesc" as never)}</p>
          </div>
          <MerchantUsagePackagesPanel
            lang={lang}
            tenantId={MOCK_MERCHANT_PREVIEW.tenantId}
            merchantName={MOCK_MERCHANT_PREVIEW.merchantName}
          />
          <MerchantReflectionOrders lang={lang} tenantId={MOCK_MERCHANT_PREVIEW.tenantId} />
        </div>
      ) : activeTab === "dashboard" ? (
        <div className="space-y-4" data-testid="merchant-dashboard-view">
          <div>
            <h2 className="text-lg font-semibold">{t("merchantDashTitle" as never)}</h2>
            <p className="text-sm text-muted-foreground">{t("merchantDashDesc" as never)}</p>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>{t("merchantDashActiveServices" as never)}</CardDescription>
                <CardTitle className="flex items-center gap-2 text-3xl tabular-nums">
                  <Sparkles className="h-5 w-5 text-teal-600" aria-hidden="true" />
                  {activeServiceCount}
                </CardTitle>
              </CardHeader>
              <CardContent>
                <Button size="sm" variant="outline" onClick={() => setTab("services")}>
                  {t("navMerchantServices" as never)}
                </Button>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>{t("merchantDashOpenOrders" as never)}</CardDescription>
                <CardTitle className="flex items-center gap-2 text-3xl tabular-nums">
                  <PackageCheck className="h-5 w-5 text-indigo-600" aria-hidden="true" />
                  {openOrderCount}
                </CardTitle>
              </CardHeader>
              <CardContent>
                <Button size="sm" variant="outline" onClick={() => setTab("orders")}>
                  {t("navMerchantOrders" as never)}
                </Button>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>{t("merchantDashBrowseCta" as never)}</CardDescription>
                <CardTitle className="flex items-center gap-2 text-3xl">
                  <ShoppingBag className="h-5 w-5 text-primary" aria-hidden="true" />
                </CardTitle>
              </CardHeader>
              <CardContent>
                <Button size="sm" onClick={() => setTab("browse")} data-testid="dash-browse-cta">
                  {t("navMerchantBrowse" as never)}
                </Button>
              </CardContent>
            </Card>
          </div>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">{t("merchantPreviewTenantTitle" as never)}</CardTitle>
              <CardDescription>{t("merchantPreviewTenantDesc" as never)}</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-2 text-sm sm:grid-cols-2">
              <div>
                <span className="text-muted-foreground">{t("merchantPreviewMerchantLabel" as never)}</span>
                <p className="font-medium">{MOCK_MERCHANT_PREVIEW.merchantName}</p>
              </div>
              <div>
                <span className="text-muted-foreground">{t("merchantPreviewTenantLabel" as never)}</span>
                <p className="font-mono text-xs">{MOCK_MERCHANT_PREVIEW.tenantId}</p>
              </div>
            </CardContent>
          </Card>
        </div>
      ) : (
        <section className="space-y-6" data-testid="merchant-browse-view">
          <div>
            <h2 className="text-lg font-semibold">{t("merchantPreviewBrowseTitle" as never)}</h2>
            <p className="text-sm text-muted-foreground">{t("merchantPreviewBrowseDesc" as never)}</p>
          </div>

          {/* Client-side filter chips — OfferingKind + partner (partner-type chips deferred: lookup gated). */}
          <div className="space-y-2" data-testid="browse-filters">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-xs font-medium text-muted-foreground">
                {t("browseFilterKindLabel" as never)}:
              </span>
              <FilterChip
                active={kindFilter === "all"}
                label={t("browseFilterAll" as never)}
                onClick={() => setKindFilter("all")}
              />
              {kindOptions.map((kind) => (
                <FilterChip
                  key={kind}
                  active={kindFilter === kind}
                  label={offeringKindLabel(lang, kind as never)}
                  onClick={() => setKindFilter(kind)}
                  testId={`kind-chip-${kind}`}
                />
              ))}
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-xs font-medium text-muted-foreground">
                {t("browseFilterPartnerLabel" as never)}:
              </span>
              <FilterChip
                active={partnerFilter === "all"}
                label={t("browseFilterAll" as never)}
                onClick={() => setPartnerFilter("all")}
              />
              {partnerOptions.map((p) => (
                <FilterChip
                  key={p.id}
                  active={partnerFilter === p.id}
                  label={p.name}
                  onClick={() => setPartnerFilter(p.id)}
                  testId={`partner-chip-${p.id}`}
                />
              ))}
            </div>
          </div>

          {loading ? (
            <CardGridSkeleton count={3} />
          ) : loadError ? (
            <ErrorState
              message={t("stateErrorGeneric" as never)}
              retryLabel={t("stateRetry" as never)}
              onRetry={() => void loadBrowse()}
            />
          ) : filteredGroups.length === 0 ? (
            <EmptyState
              icon={ShoppingBag}
              message={t("stateEmptyCatalog" as never)}
            />
          ) : (
            filteredGroups.map((group) => (
              <div key={group.moduleId} className="space-y-3">
                <h3 className="text-base font-semibold text-foreground">
                  {t(browseCategoryLabelKey(group.moduleId as never) as never)}
                </h3>
                <div className="grid gap-4 lg:grid-cols-2">
                  {group.partners.map((entry) => (
                    <PartnerBrowseCard
                      key={entry.partner.id}
                      entry={entry}
                      lang={lang}
                      activeCatalogIds={activeCatalogIds}
                      onViewDetails={(e, offering) => setDetail({ entry: e, offering })}
                      packages={packagesByPartner.get(entry.partner.id) ?? []}
                    />
                  ))}
                </div>
              </div>
            ))
          )}
        </section>
      )}

      <OfferingDetailSheet
        lang={lang}
        detail={detail}
        isActive={detail ? activeCatalogIds.has(detail.offering.id) : false}
        busy={detail ? busyCatalogId === detail.offering.id : false}
        onClose={() => setDetail(null)}
        onActivate={() => detail && setConfirmActivate(detail)}
        onOrderService={(offering) => {
          setDetail(null);
          setOrderItem(offering);
          setTab("orders");
        }}
      />

      {confirmActivate ? (
        <ConfirmDialog
          open
          dir={isRtl ? "rtl" : "ltr"}
          testId="activate-confirm"
          title={t("activateConfirmTitle" as never)}
          confirmLabel={t("confirmActivate" as never)}
          cancelLabel={t("confirmCancel" as never)}
          busy={busyCatalogId === confirmActivate.offering.id}
          onCancel={() => setConfirmActivate(null)}
          onConfirm={() => void handleActivate(confirmActivate.entry, confirmActivate.offering)}
        >
          <p className="font-medium text-foreground">{confirmActivate.offering.name}</p>
          <p>
            {t("activateConfirmPriceLabel" as never)}:{" "}
            <MoneyAmount amount={confirmActivate.offering.price.amount} />
          </p>
          <p>
            {t("browseActivationFeeLabel" as never)}: {t("browseNoActivationFee" as never)}
          </p>
          <p>{t("activateConfirmStartsNow" as never)}</p>
        </ConfirmDialog>
      ) : null}

      {confirmEndId ? (
        <ConfirmDialog
          open
          dir={isRtl ? "rtl" : "ltr"}
          testId="deactivate-confirm"
          destructive
          title={t("deactivateConfirmTitle" as never)}
          confirmLabel={t("confirmDeactivate" as never)}
          cancelLabel={t("confirmCancel" as never)}
          busy={busyCatalogId === confirmEndId}
          onCancel={() => setConfirmEndId(null)}
          onConfirm={() => void handleEnd(confirmEndId)}
        >
          <p>{t("deactivateProrationSentence" as never)}</p>
          <p>{t("deactivateReactivateSentence" as never)}</p>
        </ConfirmDialog>
      ) : null}
    </div>
  );
}

function FilterChip({
  active,
  label,
  onClick,
  testId,
}: {
  active: boolean;
  label: string;
  onClick: () => void;
  testId?: string;
}) {
  return (
    <button
      type="button"
      data-testid={testId}
      aria-pressed={active}
      onClick={onClick}
      className={[
        "rounded-full border px-3 py-1 text-xs font-medium transition-colors",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
        active
          ? "border-primary bg-primary text-primary-foreground"
          : "border-border text-muted-foreground hover:bg-muted hover:text-foreground",
      ].join(" ")}
    >
      {label}
    </button>
  );
}
