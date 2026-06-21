import { useEffect, useMemo, useState } from "react";
import { ChevronLeft, ChevronRight, Loader2, Store } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { PortalData } from "@/lib/data/types";
import type { OrgContext } from "@/lib/org/orgModel";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { useTranslator, type Lang } from "@/lib/i18n";
import { activationStatusLabel } from "@/lib/i18n/domainLabels";
import { formatTenantRef } from "@/lib/format/recordId";
import { PageHeader } from "../components/PageHeader";
import { deriveMerchants } from "../merchants/deriveMerchants";

const PLATFORM_CTX: OrgContext = { orgId: "platform", level: "Platform" };

const statusBadge: Record<string, string> = {
  Active: "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300",
  Suspended: "bg-amber-500/15 text-amber-700 dark:text-amber-300",
  Pending: "bg-sky-500/15 text-sky-700 dark:text-sky-300",
  Ended: "bg-muted text-muted-foreground",
};

export function MerchantsListPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const navigate = useNavigate();
  const { orgCtx } = usePortalSession();
  const [data, setData] = useState<PortalData | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void getPortalDataSource()
      .getScopedData(orgCtx ?? PLATFORM_CTX)
      .then(setData)
      .finally(() => setLoading(false));
  }, [orgCtx?.orgId, orgCtx?.level]);

  const merchants = useMemo(() => (data ? deriveMerchants(data) : []), [data]);
  const Caret = isRtl ? ChevronLeft : ChevronRight;

  return (
    <div className="space-y-6">
      <PageHeader title={t("navMerchants" as never)} description={t("merchantsDesc" as never)} lang={lang} />
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            <Store className="h-5 w-5 text-primary" aria-hidden="true" />
            {t("merchantsListTitle" as never)}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-base text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : merchants.length === 0 ? (
            <p className="py-8 text-center text-base text-muted-foreground">
              {t("merchantsEmpty" as never)}
            </p>
          ) : (
            <div className="overflow-x-auto" dir={isRtl ? "rtl" : "ltr"}>
              <table className="w-full min-w-[760px] text-start text-base">
                <thead>
                  <tr className="border-b border-border text-sm text-muted-foreground">
                    <th className="px-3 py-2.5 text-start font-medium">{t("colMerchant" as never)}</th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colStatus" as never)}</th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colActiveActivations" as never)}</th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colConnectedPartners" as never)}</th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colTenant" as never)}</th>
                    <th className="px-3 py-2.5" aria-hidden="true" />
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/60">
                  {merchants.map((m) => (
                    <tr
                      key={m.tenantId}
                      className="cursor-pointer transition-colors hover:bg-muted/60"
                      onClick={() => navigate(`/merchants/${encodeURIComponent(m.tenantId)}`)}
                    >
                      <td className="px-3 py-3 text-start font-medium">{m.name}</td>
                      <td className="px-3 py-3 text-start">
                        <span
                          className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${
                            statusBadge[m.status] ?? "bg-muted text-muted-foreground"
                          }`}
                        >
                          {activationStatusLabel(lang, m.status)}
                        </span>
                      </td>
                      <td className="px-3 py-3 text-start tabular-nums">
                        {m.activeActivationCount}
                        <span className="text-muted-foreground"> / {m.totalActivationCount}</span>
                      </td>
                      <td className="px-3 py-3 text-start">
                        <div className="flex flex-wrap gap-1">
                          {m.connectedPartners.map((p) => (
                            <span
                              key={p.partnerId}
                              className="inline-flex rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary"
                            >
                              {p.partnerName}
                            </span>
                          ))}
                        </div>
                      </td>
                      <td className="px-3 py-3 text-start text-sm text-muted-foreground" title={m.tenantId}>
                        {formatTenantRef(m.tenantId, lang)}
                      </td>
                      <td className="px-3 py-3 text-muted-foreground">
                        <Caret className="h-4 w-4" aria-hidden="true" />
                      </td>
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
