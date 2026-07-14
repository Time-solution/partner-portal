import { Store } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Sheet } from "@/components/ui/sheet";
import { Badge } from "@/components/ui/badge";
import { StatusBadge, OfferingKindBadge } from "@/components/catalog/badges";
import { MoneyAmount } from "@/components/MoneyAmount";
import type {
  MerchantBrowsePartnerViewEntry,
  MerchantOfferingView,
} from "@/lib/catalog/merchantBrowse";
import type { MerchantPackageDisplay } from "@/lib/usage/usagePackageDisplay";
import { PackageDetailsCard } from "./PackageDetailsCard";
import { ListingDetails } from "./ListingDetails";
import { isListingEmpty } from "@/lib/catalog/listingSchema";
import { getListing } from "@/lib/catalog/listingStore";
import { MERCHANT_PREVIEW_DESC_KEYS } from "@/lib/mock/merchantPreview";
import { partnerTypeLabel } from "@/lib/rbac/partnerNav";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { activationStatusLabel, offeringKindLabel } from "@/lib/i18n/domainLabels";

/**
 * Merchant browse card system (prop hygiene enforced): every component here receives ONLY
 * merchant-scoped shapes — MerchantOfferingView (single mapper: merchantOfferingView) and
 * MerchantPackageDisplay (single mapper: merchantPackageDisplay). partnerCost/buy/margin
 * are structurally absent from all props.
 */

/** L2 — one offering card: name, kind badge, status badge, merchant price, one primary CTA. */
export function OfferingCard({
  lang,
  offering,
  isActive,
  onDetails,
}: {
  lang: Lang;
  offering: MerchantOfferingView;
  isActive: boolean;
  onDetails: () => void;
}) {
  const t = useTranslator(lang);
  return (
    <Card className="flex flex-col" data-testid={`offering-card-${offering.id}`}>
      <CardHeader className="space-y-1.5 p-4 pb-2">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <CardTitle className="text-sm">{offering.name}</CardTitle>
          <StatusBadge
            status={isActive ? "Active" : "Published"}
            label={isActive ? t("merchantPreviewAlreadyActive" as never) : activationStatusLabel(lang, "Active")}
          />
        </div>
        <OfferingKindBadge label={offeringKindLabel(lang, offering.offeringKind)} />
      </CardHeader>
      <CardContent className="flex flex-1 flex-col justify-between gap-3 p-4 pt-1">
        <p className="text-sm font-semibold" data-testid={`offering-price-${offering.id}`}>
          <MoneyAmount amount={offering.price.amount} />
        </p>
        <Button size="sm" variant="outline" className="w-full" onClick={onDetails}
          data-testid={`offering-details-${offering.id}`}>
          {t("browseViewDetails" as never)}
        </Button>
      </CardContent>
    </Card>
  );
}

/** L1 — partner card: avatar + name + type badge + brief + offering count, L2 grid inside. */
export function PartnerBrowseCard({
  entry,
  lang,
  activeCatalogIds,
  onViewDetails,
  packages,
}: {
  entry: MerchantBrowsePartnerViewEntry;
  lang: Lang;
  activeCatalogIds: Set<string>;
  onViewDetails: (entry: MerchantBrowsePartnerViewEntry, offering: MerchantOfferingView) => void;
  /** Merchant-scoped package displays only (mapped via merchantPackageDisplay upstream). */
  packages: MerchantPackageDisplay[];
}) {
  const t = useTranslator(lang);
  const { partner, offerings } = entry;
  const name = partner.tradeName ?? partner.legalName;
  const descKey = MERCHANT_PREVIEW_DESC_KEYS[partner.id] ?? "merchantPreviewDesc_default";
  const brief = partner.partnerBrief ?? t(descKey as never);
  const benefit = offerings.find((o) => o.merchantBenefit)?.merchantBenefit;

  return (
    <Card className={["border-s-4", partner.accentClass].join(" ")} data-testid="partner-browse-card">
      <CardHeader className="space-y-2 pb-3">
        <div className="flex items-start gap-3">
          <span
            aria-hidden="true"
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-muted text-sm font-semibold"
          >
            {name.slice(0, 2)}
          </span>
          <div className="min-w-0 flex-1 space-y-1">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <CardTitle className="text-lg">{name}</CardTitle>
              <Store className="h-5 w-5 shrink-0 text-muted-foreground" aria-hidden="true" />
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline" data-testid="partner-type-badge">
                {partnerTypeLabel(partner.type, lang)}
              </Badge>
              <Badge variant="muted">
                {t("browseOfferingCount" as never)}: {offerings.length}
              </Badge>
            </div>
          </div>
        </div>
        <CardDescription className="whitespace-pre-line text-sm" data-testid="browse-partner-brief">
          {brief}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
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

        <div className="grid gap-3 sm:grid-cols-2">
          {offerings.map((offering) => (
            <OfferingCard
              key={offering.id}
              lang={lang}
              offering={offering}
              isActive={activeCatalogIds.has(offering.id)}
              onDetails={() => onViewDetails(entry, offering)}
            />
          ))}
        </div>

        {packages.length > 0 ? (
          <div className="space-y-2 border-t border-border/60 pt-3" data-testid="browse-partner-packages">
            <p className="text-xs font-medium text-muted-foreground">
              {t("merchantBrowsePackagesLabel" as never)}
            </p>
            {packages.map((display) => (
              <PackageDetailsCard key={display.id} lang={lang} display={display} />
            ))}
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

/** L3 — offering detail sheet: description, VAT-inclusive price, fee note, listing, Activate. */
export function OfferingDetailSheet({
  lang,
  detail,
  isActive,
  busy,
  onClose,
  onActivate,
  onOrderService,
}: {
  lang: Lang;
  detail: { entry: MerchantBrowsePartnerViewEntry; offering: MerchantOfferingView } | null;
  isActive: boolean;
  busy: boolean;
  onClose: () => void;
  onActivate: () => void;
  onOrderService: (offering: MerchantOfferingView) => void;
}) {
  const t = useTranslator(lang);
  if (!detail) return null;
  const { offering } = detail;
  const listing = getListing(offering.id);
  const hasListing = !isListingEmpty(listing);

  return (
    <Sheet
      open
      onClose={onClose}
      dir={lang === "ar" ? "rtl" : "ltr"}
      title={offering.name}
      footer={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">{t("browseVatNote" as never)}</p>
          <Button
            className="w-full"
            disabled={isActive || busy}
            onClick={onActivate}
            data-testid="sheet-activate"
          >
            {isActive ? t("merchantPreviewAlreadyActive" as never) : t("merchantPreviewActivate" as never)}
          </Button>
        </div>
      }
    >
      <div className="flex flex-wrap items-center gap-2">
        <OfferingKindBadge label={offeringKindLabel(lang, offering.offeringKind)} />
        <StatusBadge
          status={isActive ? "Active" : "Published"}
          label={isActive ? t("merchantPreviewAlreadyActive" as never) : activationStatusLabel(lang, "Active")}
        />
      </div>

      {offering.description ? (
        <p className="whitespace-pre-line text-sm">{offering.description}</p>
      ) : null}
      {offering.merchantBenefit ? (
        <div className="rounded-md border border-teal-500/30 bg-teal-500/5 px-3 py-2">
          <p className="text-xs font-medium text-teal-700 dark:text-teal-300">
            {t("merchantBrowseWhatYouGet" as never)}
          </p>
          <p className="mt-0.5 text-sm">{offering.merchantBenefit}</p>
        </div>
      ) : null}

      <div className="rounded-md border border-border p-3">
        <p className="text-xs text-muted-foreground">{t("merchantBrowseYourPrice" as never)}</p>
        <p className="text-lg font-semibold" data-testid="sheet-price">
          <MoneyAmount amount={offering.price.amount} />
        </p>
        <p className="mt-1 text-xs text-muted-foreground">
          {t("browseActivationFeeLabel" as never)}: {t("browseNoActivationFee" as never)}
        </p>
      </div>

      {hasListing ? (
        <div className="space-y-2 border-t border-border/60 pt-3" data-testid="browse-offering-listing">
          <ListingDetails lang={lang} listing={listing} />
          <Button
            size="sm"
            variant="outline"
            data-testid={`order-service-${offering.id}`}
            onClick={() => onOrderService(offering)}
          >
            {t("orderThisService" as never)}
          </Button>
        </div>
      ) : null}
    </Sheet>
  );
}
