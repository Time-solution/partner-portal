import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ChevronRight, Loader2 } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { Partner } from "@/lib/data/types";
import { partnerTypeLabel } from "@/lib/rbac/partnerNav";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

export function PartnersListPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const [partners, setPartners] = useState<Partner[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void getPortalDataSource()
      .getPartners()
      .then(setPartners)
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="space-y-6">
      <PageHeader title={t("navPartners" as never)} description={t("partnersDescription" as never)} lang={lang} />
      <Card>
        <CardHeader>
          <CardTitle>{t("partnersListTitle" as never)}</CardTitle>
          <CardDescription>{t("partnersListDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : (
            <ul className="divide-y divide-border">
              {partners.map((p) => (
                <li key={p.id}>
                  <Link
                    to={`/partners/${p.id}`}
                    className={`flex items-center gap-4 border-s-4 py-4 ps-4 transition-colors hover:bg-muted/50 ${p.accentClass}`}
                  >
                    <div className="min-w-0 flex-1">
                      <p className="font-medium">{p.tradeName ?? p.legalName}</p>
                      <p className="text-xs text-muted-foreground">
                        {partnerTypeLabel(p.type, lang)} · {p.participationMode}
                      </p>
                    </div>
                    <span className="rounded-full bg-muted px-2 py-0.5 text-xs">{p.status}</span>
                    <ChevronRight className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
