import { useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { ReflectedPartnerOrder } from "@/lib/data/types";
import { PageHeader } from "../components/PageHeader";
import { useTranslator } from "@/lib/i18n";
import { posSyncStatusLabel } from "@/lib/i18n/domainLabels";
import type { ModuleScopeProps } from "../moduleScope";
import { filterByPartnerIds, useScopePartnerIds } from "../hooks/useScopePartnerIds";

export function ReflectedOrdersPage({ lang, moduleId, partnerId, financeMode }: ModuleScopeProps) {
  const t = useTranslator(lang);
  const scopeIds = useScopePartnerIds(moduleId, partnerId);
  const [orders, setOrders] = useState<ReflectedPartnerOrder[]>([]);
  const [loading, setLoading] = useState(true);
  const showHeader = !moduleId && !partnerId && !financeMode;

  useEffect(() => {
    void getPortalDataSource()
      .getReflectedOrders(partnerId)
      .then((all) => filterByPartnerIds(all, scopeIds))
      .then(setOrders)
      .finally(() => setLoading(false));
  }, [partnerId, scopeIds?.join(",")]);

  return (
    <div className="space-y-6">
      {showHeader ? (
        <PageHeader
          title={t("navReflectedOrders" as never)}
          description={t("reflectedDesc" as never)}
          lang={lang}
        />
      ) : null}
      <Card>
        <CardHeader>
          <CardTitle>{t("reflectedListTitle" as never)}</CardTitle>
          <CardDescription>{t("reflectedNoMoney" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px] text-sm">
                <thead>
                  <tr className="border-b border-border text-start text-muted-foreground">
                    <th className="px-2 py-2 font-medium">{t("colPartner" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colMerchant" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colOrder" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colPosSync" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colReflectedAt" as never)}</th>
                  </tr>
                </thead>
                <tbody>
                  {orders.map((o) => (
                    <tr key={o.id} className="border-b border-border/60">
                      <td className="px-2 py-2">{o.partnerName}</td>
                      <td className="px-2 py-2">{o.merchantName}</td>
                      <td className="px-2 py-2 font-mono text-xs">{o.externalTransactionId}</td>
                      <td className="px-2 py-2">
                        <span className="rounded bg-muted px-2 py-0.5 text-xs">
                          {posSyncStatusLabel(lang, o.posSyncStatus)}
                        </span>
                      </td>
                      <td className="px-2 py-2 text-muted-foreground">
                        {new Date(o.reflectedAt).toLocaleString(lang === "ar" ? "ar-SA" : "en-GB")}
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
