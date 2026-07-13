import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { ChevronDown, ChevronUp, Loader2, Sparkles, Store } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { MerchantReflectionOrders } from "@/features/merchant/MerchantReflectionOrders";
import {
  browseCategoryLabelKey,
  buildMerchantBrowseGroups,
  merchantListedSellPrice,
  type MerchantBrowsePartnerEntry,
} from "@/lib/catalog/merchantBrowse";
import { projectCatalogPrice } from "@/lib/catalog/catalogPriceVisibility";
import { getPortalDataSource } from "@/lib/data";
import type { PartnerCatalogItem } from "@/lib/data/types";
import { listUsagePackages } from "@/lib/usage/usagePackageStore";
import type { UsagePackage } from "@/lib/usage/usagePackage";
import { PackageDetailsCard } from "../components/PackageDetailsCard";
import { ListingDetails } from "../components/ListingDetails";
import { isListingEmpty, type OfferingListing } from "@/lib/catalog/listingSchema";
import { getListing } from "@/lib/catalog/listingStore";
import { MerchantServiceOrdersPanel, ServiceOrderForm } from "../components/ServiceOrderPanels";
import { subscribePortalDataChanged } from "@/lib/data/portalDataEvents";
import {
  MERCHANT_PREVIEW_DESC_KEYS,
  MOCK_MERCHANT_PREVIEW,
} from "@/lib/mock/merchantPreview";
import { OrgProfileForm } from "@/features/settings/profile/OrgProfileForm";
import { PageHeader } from "../components/PageHeader";
import { MerchantActivePartnersPanel } from "../components/MerchantActivePartnersPanel";
import { MerchantUsagePackagesPanel } from "../components/MerchantUsagePackagesPanel";
import { MerchantStatementView } from "@/features/merchant/MerchantStatementView";
import { tabBarClass, tabLinkClass } from "@/lib/ui/tabs";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { offeringKindLabel } from "@/lib/i18n/domainLabels";

type MerchantTab = "partners" | "browse" | "profile" | "statement";

function parseMerchantTab(raw: string | null): MerchantTab {
  if (raw === "browse" || raw === "profile" || raw === "statement") return raw;
  return "partners";
}

function TierPrice({ item }: { item: PartnerCatalogItem }) {
  const sell = merchantListedSellPrice(item);
  const scoped = projectCatalogPrice({ buy: item.partnerCost, sell }, "merchant");
  if (!scoped.sell) return null;
  return <MoneyAmount amount={scoped.sell.amount} />;
}

export function PartnerBrowseCard({
  entry,
  lang,
  expanded,
  onToggleExpand,
  activeCatalogIds,
  busyCatalogId,
  onActivate,
  packages,
  listings,
  onOrderService,
}: {
  entry: MerchantBrowsePartnerEntry;
  lang: Lang;
  expanded: boolean;
  onToggleExpand: () => void;
  activeCatalogIds: Set<string>;
  busyCatalogId: string | null;
  onActivate: (entry: MerchantBrowsePartnerEntry, item: PartnerCatalogItem) => void;
  packages: UsagePackage[];
  /** Gate 2a — structured listings keyed by offering id (read-only merchant rendering). */
  listings?: Record<string, OfferingListing>;
  /** Gate 2b — start a service order from the listing view. */
  onOrderService?: (item: PartnerCatalogItem) => void;
}) {
  const t = useTranslator(lang);
  const { partner, offerings, hasTierMenu } = entry;
  const name = partner.tradeName ?? partner.legalName;
  const descKey = MERCHANT_PREVIEW_DESC_KEYS[partner.id] ?? "merchantPreviewDesc_default";
  const serviceType = offerings[0]
    ? offeringKindLabel(lang, offerings[0].offeringKind)
    : "—";
  // Lead the card with the partner's own company brief; fall back to the mock catalogue copy.
  const brief = partner.partnerBrief ?? t(descKey as never);
  const benefit = offerings.find((o) => o.merchantBenefit)?.merchantBenefit;

  const singleOffering = offerings.length === 1 ? offerings[0] : undefined;
  const singleActive = singleOffering ? activeCatalogIds.has(singleOffering.id) : false;

  return (
    <Card className={["border-s-4", partner.accentClass].join(" ")} data-testid="partner-browse-card">
      <CardHeader className="space-y-1 pb-3">
        <div className="flex items-start justify-between gap-2">
          <CardTitle className="text-lg">{name}</CardTitle>
          <Store className="h-5 w-5 shrink-0 text-muted-foreground" aria-hidden="true" />
        </div>
        <CardDescription className="text-sm">{serviceType}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div data-testid="browse-partner-brief">
          <p className="text-xs font-medium text-muted-foreground">{t("merchantBrowseAboutPartner" as never)}</p>
          <p className="mt-0.5 whitespace-pre-line text-sm">{brief}</p>
        </div>
        {benefit ? (
          <div
            data-testid="browse-merchant-benefit"
            className="rounded-md border border-teal-500/30 bg-teal-500/5 px-3 py-2"
          >
            <p className="text-xs font-medium text-teal-700 dark:text-teal-300">
              {t("merchantBrowseWhatYouGet" as never)}
            </p>
            <p className="mt-0.5 text-sm">{benefit}</p>
          </div>
        ) : null}
        {hasTierMenu ? (
          <>
            <Button variant="outline" className="w-full justify-between" onClick={onToggleExpand}>
              {expanded ? t("merchantBrowseHideTiers" as never) : t("merchantBrowseChooseTier" as never)}
              {expanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
            </Button>
            {expanded ? (
              <ul className="space-y-2">
                {offerings.map((item) => {
                  const isActive = activeCatalogIds.has(item.id);
                  return (
                    <li
                      key={item.id}
                      className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border px-3 py-2"
                    >
                      <div>
                        <p className="font-medium">{item.name}</p>
                        <p className="text-xs text-muted-foreground">{item.code}</p>
                      </div>
                      <div className="flex items-center gap-2">
                        <TierPrice item={item} />
                        <Button
                          size="sm"
                          disabled={isActive || busyCatalogId === item.id}
                          onClick={() => onActivate(entry, item)}
                        >
                          {isActive
                            ? t("merchantPreviewAlreadyActive" as never)
                            : t("merchantPreviewActivate" as never)}
                        </Button>
                      </div>
                    </li>
                  );
                })}
              </ul>
            ) : null}
          </>
        ) : singleOffering ? (
          <>
            <p className="text-sm text-muted-foreground">{singleOffering.name}</p>
            <p className="text-sm font-medium">
              {t("merchantBrowseYourPrice" as never)}: <TierPrice item={singleOffering} />
            </p>
            <Button
              className="w-full"
              disabled={singleActive || busyCatalogId === singleOffering.id}
              onClick={() => onActivate(entry, singleOffering)}
            >
              {singleActive
                ? t("merchantPreviewAlreadyActive" as never)
                : t("merchantPreviewActivate" as never)}
            </Button>
          </>
        ) : null}

        {listings
          ? offerings
              .filter((item) => listings[item.id] && !isListingEmpty(listings[item.id]))
              .map((item) => (
                <div key={item.id} className="space-y-2 border-t border-border/60 pt-3" data-testid="browse-offering-listing">
                  <ListingDetails lang={lang} listing={listings[item.id]} />
                  {onOrderService ? (
                    <Button size="sm" data-testid={`order-service-${item.id}`} onClick={() => onOrderService(item)}>
                      {t("orderThisService" as never)}
                    </Button>
                  ) : null}
                </div>
              ))
          : null}

        {packages.length > 0 ? (
          <div className="space-y-2 border-t border-border/60 pt-3" data-testid="browse-partner-packages">
            <p className="text-xs font-medium text-muted-foreground">
              {t("merchantBrowsePackagesLabel" as never)}
            </p>
            {packages.map((pkg) => (
              <PackageDetailsCard key={pkg.id} lang={lang} pkg={pkg} />
            ))}
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

export function MerchantPreviewPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = parseMerchantTab(searchParams.get("tab"));
  const [browseGroups, setBrowseGroups] = useState(() => buildMerchantBrowseGroups([], []));
  const [packagesByPartner, setPackagesByPartner] = useState<Map<string, UsagePackage[]>>(new Map());
  const [activeCatalogIds, setActiveCatalogIds] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [expandedPartnerId, setExpandedPartnerId] = useState<string | null>(null);
  const [busyCatalogId, setBusyCatalogId] = useState<string | null>(null);
  const [orderItem, setOrderItem] = useState<PartnerCatalogItem | null>(null);
  const [ordersVersion, setOrdersVersion] = useState(0);
  const [toast, setToast] = useState<string | null>(null);

  const loadBrowse = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [partners, catalog, activations] = await Promise.all([
        ds.getPartners(),
        ds.getCatalogItems(),
        ds.getActivations(),
      ]);
      setBrowseGroups(buildMerchantBrowseGroups(partners, catalog));
      const pkgMap = new Map<string, UsagePackage[]>();
      for (const pkg of listUsagePackages().filter((p) => p.status === "Published")) {
        pkgMap.set(pkg.partnerId, [...(pkgMap.get(pkg.partnerId) ?? []), pkg]);
      }
      setPackagesByPartner(pkgMap);
      const ids = new Set<string>();
      for (const row of activations) {
        if (
          row.activation.tenantId === MOCK_MERCHANT_PREVIEW.tenantId &&
          row.activation.status !== "Ended"
        ) {
          ids.add(row.activation.catalogItemId);
        }
      }
      setActiveCatalogIds(ids);
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
    const timer = window.setTimeout(() => setToast(null), 5000);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const setTab = (tab: MerchantTab) => {
    if (tab === "partners") {
      setSearchParams({});
    } else {
      setSearchParams({ tab });
    }
  };

  const handleActivate = async (entry: MerchantBrowsePartnerEntry, item: PartnerCatalogItem) => {
    setBusyCatalogId(item.id);
    try {
      const sell = merchantListedSellPrice(item);
      await getPortalDataSource().createActivation({
        partnerId: entry.partner.id,
        catalogItemId: item.id,
        tenantId: MOCK_MERCHANT_PREVIEW.tenantId,
        merchantName: MOCK_MERCHANT_PREVIEW.merchantName,
        resalePrice: sell,
      });
      setToast(t("merchantBrowseActivatedToast" as never));
      setExpandedPartnerId(null);
      await loadBrowse();
    } catch (err) {
      setToast(err instanceof Error ? err.message : String(err));
    } finally {
      setBusyCatalogId(null);
    }
  };

  const handleEnd = async (activationId: string) => {
    setBusyCatalogId(activationId);
    try {
      await getPortalDataSource().endActivation(activationId);
      await loadBrowse();
    } finally {
      setBusyCatalogId(null);
    }
  };

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

      <nav className={tabBarClass} aria-label={t("merchantPreviewNav" as never)}>
        <button
          type="button"
          className={tabLinkClass(activeTab === "partners")}
          onClick={() => setTab("partners")}
        >
          {t("merchantTabPartners" as never)}
        </button>
        <button
          type="button"
          className={tabLinkClass(activeTab === "statement")}
          onClick={() => setTab("statement")}
        >
          {t("merchantTabStatement" as never)}
        </button>
        <button
          type="button"
          className={tabLinkClass(activeTab === "browse")}
          onClick={() => setTab("browse")}
        >
          {t("merchantTabBrowse" as never)}
        </button>
        <button
          type="button"
          className={tabLinkClass(activeTab === "profile")}
          onClick={() => setTab("profile")}
        >
          {t("merchantTabProfile" as never)}
        </button>
      </nav>

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
      ) : activeTab === "partners" ? (
        <MerchantActivePartnersPanel
          lang={lang}
          tenantId={MOCK_MERCHANT_PREVIEW.tenantId}
          onDeactivate={(id) => void handleEnd(id)}
          busyActivationId={busyCatalogId}
        />
      ) : (
        <>
          {toast ? (
            <div
              role="alert"
              className="flex items-start gap-3 rounded-lg border border-teal-500/40 bg-teal-500/10 px-4 py-3 text-sm text-teal-950 dark:text-teal-50"
            >
              <Sparkles className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
              <span>{toast}</span>
            </div>
          ) : null}

          {orderItem ? (
            <ServiceOrderForm
              lang={lang}
              catalogItemId={orderItem.id}
              offeringName={orderItem.name}
              partnerId={orderItem.partnerId}
              tenantId={MOCK_MERCHANT_PREVIEW.tenantId}
              participationMode={orderItem.participationMode === "SubscriptionFee" ? "SubscriptionFee" : "Principal"}
              buy={orderItem.partnerCost.amount}
              sellOrFee={orderItem.partnerCost.amount}
              requirements={getListing(orderItem.id).requirements}
              onDone={() => {
                setOrderItem(null);
                setOrdersVersion((v) => v + 1);
              }}
              onCancel={() => setOrderItem(null)}
            />
          ) : null}
          <MerchantServiceOrdersPanel key={ordersVersion} lang={lang} tenantId={MOCK_MERCHANT_PREVIEW.tenantId} />
          <MerchantReflectionOrders lang={lang} tenantId={MOCK_MERCHANT_PREVIEW.tenantId} />

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

          <section className="space-y-6">
            <div>
              <h2 className="text-lg font-semibold">{t("merchantPreviewBrowseTitle" as never)}</h2>
              <p className="text-sm text-muted-foreground">{t("merchantPreviewBrowseDesc" as never)}</p>
            </div>

            {loading ? (
              <div className="flex items-center gap-2 text-muted-foreground">
                <Loader2 className="h-5 w-5 animate-spin" />
                {t("loadingData" as never)}
              </div>
            ) : (
              browseGroups.map((group) => (
                <div key={group.moduleId} className="space-y-3">
                  <h3 className="text-base font-semibold text-foreground">
                    {t(browseCategoryLabelKey(group.moduleId) as never)}
                  </h3>
                  <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                    {group.partners.map((entry) => (
                      <PartnerBrowseCard
                        key={entry.partner.id}
                        entry={entry}
                        lang={lang}
                        expanded={expandedPartnerId === entry.partner.id}
                        onToggleExpand={() =>
                          setExpandedPartnerId((id) =>
                            id === entry.partner.id ? null : entry.partner.id,
                          )
                        }
                        activeCatalogIds={activeCatalogIds}
                        listings={Object.fromEntries(
                          entry.offerings.map((item) => [item.id, getListing(item.id)]),
                        )}
                        onOrderService={(item) => setOrderItem(item)}
                        busyCatalogId={busyCatalogId}
                        onActivate={handleActivate}
                        packages={packagesByPartner.get(entry.partner.id) ?? []}
                      />
                    ))}
                  </div>
                </div>
              ))
            )}
          </section>

          <MerchantUsagePackagesPanel
            lang={lang}
            tenantId={MOCK_MERCHANT_PREVIEW.tenantId}
            merchantName={MOCK_MERCHANT_PREVIEW.merchantName}
          />
        </>
      )}
    </div>
  );
}
