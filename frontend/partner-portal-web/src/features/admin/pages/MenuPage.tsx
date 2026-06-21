import { useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { getPortalDataSource } from "@/lib/data";
import type { PartnerCatalogItem } from "@/lib/data/types";
import { PageHeader } from "../components/PageHeader";
import { EmptyState } from "../components/EmptyState";
import { useTranslator } from "@/lib/i18n";
import { offeringKindLabel } from "@/lib/i18n/domainLabels";
import type { ModuleScopeProps } from "../moduleScope";
import { filterByPartnerIds, useScopePartnerIds } from "../hooks/useScopePartnerIds";

/** F&B module — menu items (catalog scoped to F&B partners). */
export function MenuPage({ lang, moduleId, partnerId }: ModuleScopeProps) {
  const t = useTranslator(lang);
  const scopeIds = useScopePartnerIds(moduleId, partnerId);
  const [items, setItems] = useState<PartnerCatalogItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void getPortalDataSource()
      .getCatalogItems(partnerId)
      .then((all) => filterByPartnerIds(all, scopeIds))
      .then(setItems)
      .finally(() => setLoading(false));
  }, [partnerId, scopeIds?.join(",")]);

  return (
    <div className="space-y-6">
      {!moduleId && !partnerId ? (
        <PageHeader title={t("moduleScreen_menu" as never)} description={t("menuDesc" as never)} lang={lang} />
      ) : null}
      <Card>
        <CardHeader>
          <CardTitle>{t("menuTitle" as never)}</CardTitle>
          <CardDescription>{t("menuDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : items.length === 0 ? (
            <EmptyState message={t("menuEmpty" as never)} />
          ) : (
            <ul className="divide-y divide-border text-sm">
              {items.map((item) => (
                <li key={item.id} className="flex flex-wrap gap-2 py-3">
                  <span className="font-medium">{item.name}</span>
                  <span className="text-muted-foreground">({item.code})</span>
                  <span className="rounded bg-muted px-2 py-0.5 text-xs">
                    {offeringKindLabel(lang, item.offeringKind)}
                  </span>
                  <span className="ms-auto tabular-nums">
                    <MoneyAmount amount={item.partnerCost.amount} />
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
