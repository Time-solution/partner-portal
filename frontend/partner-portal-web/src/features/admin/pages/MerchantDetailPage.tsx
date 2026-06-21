import { useEffect, useMemo, useState } from "react";
import { ArrowLeft, ArrowRight, Loader2, Store } from "lucide-react";
import { Link, useParams } from "react-router-dom";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { ActivationStatus, PortalData } from "@/lib/data/types";
import type { OrgContext } from "@/lib/org/orgModel";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { useTranslator, type Lang } from "@/lib/i18n";
import {
  activationStatusLabel,
  offeringKindLabel,
  participationModeLabel,
} from "@/lib/i18n/domainLabels";
import { formatTenantRef } from "@/lib/format/recordId";
import { deriveMerchants, type MerchantSummary } from "../merchants/deriveMerchants";

const PLATFORM_CTX: OrgContext = { orgId: "platform", level: "Platform" };

const ACTIVATION_FLOW: ActivationStatus[] = ["Pending", "Active", "Suspended", "Ended"];

const statusBadge: Record<string, string> = {
  Active: "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300",
  Suspended: "bg-amber-500/15 text-amber-700 dark:text-amber-300",
  Pending: "bg-sky-500/15 text-sky-700 dark:text-sky-300",
  Ended: "bg-muted text-muted-foreground",
};

function StatusFlow({ current, lang }: { current: ActivationStatus; lang: Lang }) {
  const isRtl = lang === "ar";
  const Arrow = isRtl ? ArrowLeft : ArrowRight;
  return (
    <div className="flex flex-wrap items-center gap-1.5" dir={isRtl ? "rtl" : "ltr"}>
      {ACTIVATION_FLOW.map((stage, i) => {
        const active = stage === current;
        return (
          <span key={stage} className="flex items-center gap-1.5">
            <span
              className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${
                active ? statusBadge[stage] : "bg-muted/50 text-muted-foreground"
              }`}
            >
              {activationStatusLabel(lang, stage)}
            </span>
            {i < ACTIVATION_FLOW.length - 1 ? (
              <Arrow className="h-3.5 w-3.5 text-muted-foreground/60" aria-hidden="true" />
            ) : null}
          </span>
        );
      })}
    </div>
  );
}

export function MerchantDetailPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const { tenantId: rawTenantId } = useParams<{ tenantId: string }>();
  const tenantId = rawTenantId ? decodeURIComponent(rawTenantId) : "";
  const { orgCtx } = usePortalSession();
  const [data, setData] = useState<PortalData | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void getPortalDataSource()
      .getScopedData(orgCtx ?? PLATFORM_CTX)
      .then(setData)
      .finally(() => setLoading(false));
  }, [orgCtx?.orgId, orgCtx?.level]);

  const merchant: MerchantSummary | undefined = useMemo(
    () => (data ? deriveMerchants(data).find((m) => m.tenantId === tenantId) : undefined),
    [data, tenantId],
  );

  const partnersById = useMemo(
    () => new Map((data?.partners ?? []).map((p) => [p.id, p])),
    [data],
  );
  const catalogById = useMemo(
    () => new Map((data?.catalogItems ?? []).map((c) => [c.id, c])),
    [data],
  );

  const BackArrow = isRtl ? ArrowRight : ArrowLeft;

  return (
    <div className="space-y-6">
      <Link
        to="/merchants"
        className="inline-flex items-center gap-2 text-sm font-medium text-muted-foreground transition-colors hover:text-foreground"
      >
        <BackArrow className="h-4 w-4" aria-hidden="true" />
        {t("merchantBackToList" as never)}
      </Link>

      {loading ? (
        <div className="flex items-center gap-2 py-8 text-base text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          {t("loadingData" as never)}
        </div>
      ) : !merchant ? (
        <p className="py-8 text-center text-base text-muted-foreground">{t("merchantNotFound" as never)}</p>
      ) : (
        <>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <span className="flex h-11 w-11 items-center justify-center rounded-lg bg-primary/10 text-primary">
                <Store className="h-6 w-6" aria-hidden="true" />
              </span>
              <div>
                <h2 className="text-2xl font-bold">{merchant.name}</h2>
                <p className="text-sm text-muted-foreground" title={merchant.tenantId}>
                  {formatTenantRef(merchant.tenantId, lang)}
                </p>
              </div>
            </div>
            <span
              className={`inline-flex rounded-full px-3 py-1 text-sm font-medium ${
                statusBadge[merchant.status] ?? "bg-muted text-muted-foreground"
              }`}
            >
              {activationStatusLabel(lang, merchant.status)}
            </span>
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">{t("merchantConnectedPartners" as never)}</CardTitle>
              <CardDescription>{t("merchantConnectedPartnersDesc" as never)}</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                {merchant.connectedPartners.map((p) => (
                  <div
                    key={p.partnerId}
                    className="flex items-center justify-between gap-2 rounded-lg border border-border p-3"
                  >
                    <span className="font-medium">{p.partnerName}</span>
                    <span className="inline-flex rounded-full bg-violet-500/15 px-2.5 py-0.5 text-xs font-medium text-violet-700 dark:text-violet-300">
                      {participationModeLabel(lang, p.participationMode)}
                    </span>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">{t("merchantActivations" as never)}</CardTitle>
              <CardDescription>{t("merchantActivationsDesc" as never)}</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="overflow-x-auto" dir={isRtl ? "rtl" : "ltr"}>
                <table className="w-full min-w-[820px] text-start text-base">
                  <thead>
                    <tr className="border-b border-border text-sm text-muted-foreground">
                      <th className="px-3 py-2.5 text-start font-medium">{t("colPartner" as never)}</th>
                      <th className="px-3 py-2.5 text-start font-medium">{t("colType" as never)}</th>
                      <th className="px-3 py-2.5 text-start font-medium">{t("colStatus" as never)}</th>
                      <th className="px-3 py-2.5 text-start font-medium">{t("colActivatedAt" as never)}</th>
                      <th className="px-3 py-2.5 text-start font-medium">{t("colStatusFlow" as never)}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border/60">
                    {merchant.activations.map((a) => {
                      const partner = partnersById.get(a.partnerId);
                      const catalog = catalogById.get(a.catalogItemId);
                      return (
                        <tr key={a.id}>
                          <td className="px-3 py-3 text-start font-medium">
                            {partner?.tradeName ?? partner?.legalName ?? a.partnerId}
                          </td>
                          <td className="px-3 py-3 text-start">
                            {catalog
                              ? offeringKindLabel(lang, catalog.offeringKind)
                              : a.catalogItemName}
                          </td>
                          <td className="px-3 py-3 text-start">
                            <span
                              className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${
                                statusBadge[a.status] ?? "bg-muted text-muted-foreground"
                              }`}
                            >
                              {activationStatusLabel(lang, a.status)}
                            </span>
                          </td>
                          <td className="px-3 py-3 text-start text-sm text-muted-foreground">
                            {a.activatedAt
                              ? new Date(a.activatedAt).toLocaleDateString(
                                  lang === "ar" ? "ar-SA" : "en-GB",
                                )
                              : "—"}
                          </td>
                          <td className="px-3 py-3 text-start">
                            <StatusFlow current={a.status} lang={lang} />
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
