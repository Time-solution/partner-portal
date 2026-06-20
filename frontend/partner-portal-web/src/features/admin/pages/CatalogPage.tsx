import { useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { PartnerCatalogItem } from "@/lib/data/types";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

export function CatalogPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const [items, setItems] = useState<PartnerCatalogItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void getPortalDataSource()
      .getCatalogItems()
      .then(setItems)
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="space-y-6">
      <PageHeader title={t("navCatalog" as never)} description={t("catalogDesc" as never)} lang={lang} />
      <Card>
        <CardHeader>
          <CardTitle>{t("catalogAllTitle" as never)}</CardTitle>
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
              <table className="w-full min-w-[640px] text-sm">
                <thead>
                  <tr className="border-b border-border text-start text-muted-foreground">
                    <th className="px-2 py-2 font-medium">{t("colItem" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colOffering" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colMode" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("colBook" as never)}</th>
                    <th className="px-2 py-2 text-end font-medium">{t("colCost" as never)}</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.id} className="border-b border-border/60">
                      <td className="px-2 py-2">
                        <span className="font-medium">{item.name}</span>
                        <span className="ms-2 text-xs text-muted-foreground">{item.code}</span>
                      </td>
                      <td className="px-2 py-2">{item.offeringKind}</td>
                      <td className="px-2 py-2">{item.participationMode}</td>
                      <td className="px-2 py-2">{item.settlementBook}</td>
                      <td className="px-2 py-2 text-end tabular-nums">{item.partnerCost.amount.toFixed(2)} SAR</td>
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
