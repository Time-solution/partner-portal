import { Fragment, useEffect, useState } from "react";
import { ChevronDown, ChevronRight, Loader2 } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ReflectionDetailPanel } from "@/features/admin/components/ReflectionDetailPanel";
import { TableEmptyRow } from "@/features/admin/components/EmptyState";
import { getPortalDataSource } from "@/lib/data";
import type { ReflectedPartnerOrder } from "@/lib/data/types";
import { formatOrderRef } from "@/lib/format/recordId";
import { useTranslator, type Lang } from "@/lib/i18n";
import { filterMerchantReflectionOrders } from "@/lib/reflection/reflectionView";

export function MerchantReflectionOrders({ lang, tenantId }: { lang: Lang; tenantId: string }) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const Caret = isRtl ? ChevronRight : ChevronDown;
  const [orders, setOrders] = useState<ReflectedPartnerOrder[]>([]);
  const [loading, setLoading] = useState(true);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  useEffect(() => {
    void getPortalDataSource()
      .getReflectedOrders()
      .then((all) => filterMerchantReflectionOrders(all, tenantId))
      .then(setOrders)
      .finally(() => setLoading(false));
  }, [tenantId]);

  const toggle = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("merchantReflectionTitle" as never)}</CardTitle>
        <CardDescription>{t("merchantReflectionDesc" as never)}</CardDescription>
      </CardHeader>
      <CardContent>
        {loading ? (
          <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            {t("loadingData" as never)}
          </div>
        ) : (
          <div className="overflow-x-auto" dir={isRtl ? "rtl" : "ltr"}>
            <table className="w-full min-w-[560px] text-start text-base">
              <thead>
                <tr className="border-b border-border text-sm text-muted-foreground">
                  <th className="w-8 px-2 py-2.5" />
                  <th className="px-3 py-2.5 text-start font-medium">
                    {t("merchantReflectionDate" as never)}
                  </th>
                  <th className="px-3 py-2.5 text-start font-medium">
                    {t("merchantReflectionOrderRef" as never)}
                  </th>
                  <th className="px-3 py-2.5 text-start font-medium">
                    {t("merchantReflectionItem" as never)}
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/60">
                {orders.length === 0 ? (
                  <TableEmptyRow colSpan={4} message={t("merchantReflectionEmpty" as never)} />
                ) : null}
                {orders.map((o) => {
                  const open = expanded.has(o.id);
                  return (
                    <Fragment key={o.id}>
                      <tr
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => toggle(o.id)}
                      >
                        <td className="px-2 py-2.5 text-muted-foreground">
                          <Caret className="h-4 w-4" />
                        </td>
                        <td className="px-3 py-2.5 text-start text-sm text-muted-foreground">
                          {new Date(o.reflectedAt).toLocaleString(lang === "ar" ? "ar-SA" : "en-GB")}
                        </td>
                        <td className="px-3 py-2.5 text-start tabular-nums" title={o.externalTransactionId}>
                          {formatOrderRef(o.externalTransactionId, lang)}
                        </td>
                        <td className="px-3 py-2.5 text-start">{o.orderLineId}</td>
                      </tr>
                      {open && o.reflection ? (
                        <tr>
                          <td colSpan={4} className="bg-muted/30 px-3 py-3">
                            <ReflectionDetailPanel order={o} lang={lang} viewer="merchant" />
                          </td>
                        </tr>
                      ) : null}
                    </Fragment>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
