import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, Loader2, Sparkles, Store } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { MerchantActivationRow, Partner, PartnerCatalogItem } from "@/lib/data/types";
import { subscribePortalDataChanged } from "@/lib/data/portalDataEvents";
import {
  MERCHANT_PREVIEW_BENEFIT_KEYS,
  MERCHANT_PREVIEW_DESC_KEYS,
  MOCK_MERCHANT_PREVIEW,
} from "@/lib/mock/merchantPreview";
import { moduleLabelKey, resolvePartnerModule } from "@/lib/rbac/partnerModules";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { activationStatusLabel } from "@/lib/i18n/domainLabels";

interface PartnerOffering {
  partner: Partner;
  catalogItem: PartnerCatalogItem;
  moduleKey: string;
  benefitKey: string;
  descKey: string;
}

export function MerchantPreviewPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const [offerings, setOfferings] = useState<PartnerOffering[]>([]);
  const [myActivations, setMyActivations] = useState<MerchantActivationRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyPartnerId, setBusyPartnerId] = useState<string | null>(null);
  const [busyActivationId, setBusyActivationId] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [partners, catalog, activations] = await Promise.all([
        ds.getPartners(),
        ds.getCatalogItems(),
        ds.getActivations(),
      ]);

      const activePartners = partners.filter((p) => p.status === "Active");
      const nextOfferings: PartnerOffering[] = [];
      for (const partner of activePartners) {
        const catalogItem = catalog.find((c) => c.partnerId === partner.id && c.status === "Active");
        if (!catalogItem) continue;
        nextOfferings.push({
          partner,
          catalogItem,
          moduleKey: moduleLabelKey(resolvePartnerModule(partner)),
          benefitKey: MERCHANT_PREVIEW_BENEFIT_KEYS[partner.id] ?? "merchantPreviewBenefit_default",
          descKey: MERCHANT_PREVIEW_DESC_KEYS[partner.id] ?? "merchantPreviewDesc_default",
        });
      }
      setOfferings(nextOfferings);
      setMyActivations(
        activations.filter((row) => row.activation.tenantId === MOCK_MERCHANT_PREVIEW.tenantId),
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
    return subscribePortalDataChanged(() => {
      void load();
    });
  }, [load]);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 5000);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const activationForPartner = useMemo(() => {
    const map = new Map<string, MerchantActivationRow>();
    for (const row of myActivations) {
      if (row.activation.status !== "Ended") {
        map.set(row.activation.partnerId, row);
      }
    }
    return map;
  }, [myActivations]);

  const handleActivate = async (offering: PartnerOffering) => {
    setBusyPartnerId(offering.partner.id);
    try {
      await getPortalDataSource().createActivation({
        partnerId: offering.partner.id,
        catalogItemId: offering.catalogItem.id,
        tenantId: MOCK_MERCHANT_PREVIEW.tenantId,
        merchantName: MOCK_MERCHANT_PREVIEW.merchantName,
      });
      setToast(t("merchantPreviewReflectToast" as never));
      await load();
    } catch (err) {
      setToast(err instanceof Error ? err.message : String(err));
    } finally {
      setBusyPartnerId(null);
    }
  };

  const handleEnd = async (activationId: string) => {
    setBusyActivationId(activationId);
    try {
      await getPortalDataSource().endActivation(activationId);
      await load();
    } finally {
      setBusyActivationId(null);
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

      {toast ? (
        <div
          role="alert"
          className="flex items-start gap-3 rounded-lg border border-teal-500/40 bg-teal-500/10 px-4 py-3 text-sm text-teal-950 dark:text-teal-50"
        >
          <Sparkles className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
          <span>{toast}</span>
        </div>
      ) : null}

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

      <section className="space-y-4">
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
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {offerings.map(({ partner, catalogItem, moduleKey, benefitKey, descKey }) => {
              const existing = activationForPartner.get(partner.id);
              const isSubscription = partner.participationMode === "SubscriptionFee";
              const name = partner.tradeName ?? partner.legalName;

              return (
                <Card key={partner.id} className={["border-s-4", partner.accentClass].join(" ")}>
                  <CardHeader className="space-y-1 pb-3">
                    <div className="flex items-start justify-between gap-2">
                      <CardTitle className="text-lg">{name}</CardTitle>
                      <Store className="h-5 w-5 shrink-0 text-muted-foreground" aria-hidden="true" />
                    </div>
                    <CardDescription className="text-sm">
                      {t(moduleKey as never)} · {t(descKey as never)}
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <p className="text-sm font-medium text-primary">
                      {t(benefitKey as never).replace(
                        "{amount}",
                        catalogItem.partnerCost.amount.toFixed(2),
                      )}
                    </p>
                    {isSubscription ? (
                      <p className="text-sm text-muted-foreground">
                        {t("merchantPreviewSubscriptionFee" as never).replace(
                          "{amount}",
                          catalogItem.partnerCost.amount.toFixed(2),
                        )}
                      </p>
                    ) : null}
                    {existing ? (
                      <p className="text-sm text-muted-foreground">
                        {activationStatusLabel(lang, existing.activation.status)}
                      </p>
                    ) : null}
                    <Button
                      className="w-full"
                      disabled={Boolean(existing) || busyPartnerId === partner.id}
                      onClick={() => void handleActivate({ partner, catalogItem, moduleKey, benefitKey, descKey })}
                    >
                      {existing
                        ? t("merchantPreviewAlreadyActive" as never)
                        : t("merchantPreviewActivate" as never)}
                    </Button>
                  </CardContent>
                </Card>
              );
            })}
          </div>
        )}
      </section>

      <section className="space-y-4">
        <div>
          <h2 className="text-lg font-semibold">{t("merchantPreviewMyPartnersTitle" as never)}</h2>
          <p className="text-sm text-muted-foreground">{t("merchantPreviewMyPartnersDesc" as never)}</p>
        </div>

        {myActivations.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("merchantPreviewMyPartnersEmpty" as never)}</p>
        ) : (
          <div className="space-y-3">
            {myActivations.map(({ activation: a }) => {
              const partnerName =
                offerings.find((o) => o.partner.id === a.partnerId)?.partner.tradeName ??
                offerings.find((o) => o.partner.id === a.partnerId)?.partner.legalName ??
                a.catalogItemName;

              return (
                <Card key={a.id}>
                  <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3 pb-2">
                    <div>
                      <CardTitle className="text-base">{partnerName}</CardTitle>
                      <CardDescription className="text-sm">
                        {a.catalogItemName} · {activationStatusLabel(lang, a.status)}
                      </CardDescription>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      {a.status === "Active" ? (
                        <span className="inline-flex items-center gap-1 rounded-full border border-teal-500/40 bg-teal-500/10 px-2.5 py-1 text-xs font-medium text-teal-800 dark:text-teal-200">
                          <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" />
                          {t("merchantPreviewReflectedBadge" as never)}
                        </span>
                      ) : null}
                      {a.status !== "Ended" ? (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={busyActivationId === a.id}
                          onClick={() => void handleEnd(a.id)}
                        >
                          {t("merchantPreviewEnd" as never)}
                        </Button>
                      ) : null}
                    </div>
                  </CardHeader>
                </Card>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
}
