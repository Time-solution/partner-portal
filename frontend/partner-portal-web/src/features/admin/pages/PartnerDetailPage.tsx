import { useEffect, useState, type ReactNode } from "react";
import { Link, Navigate, useParams } from "react-router-dom";
import { ArrowLeft, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { Partner } from "@/lib/data/types";
import { deriveSettlementSummary } from "@/lib/data/types";
import { partnerTypeLabel } from "@/lib/rbac/partnerNav";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { PartnerSubNav } from "../components/PartnerSubNav";
import { JournalTable } from "../components/JournalTable";
import { ActivationWorkflow } from "../components/ActivationWorkflow";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

export function PartnerDetailPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { id, tab } = useParams<{ id: string; tab?: string }>();
  const { scopedPartnerId } = usePortalSession();
  const [partner, setPartner] = useState<Partner | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) return;
    void getPortalDataSource()
      .getPartner(id)
      .then((p) => setPartner(p ?? null))
      .finally(() => setLoading(false));
  }, [id]);

  if (scopedPartnerId && id !== scopedPartnerId) {
    return <Navigate to={`/partners/${scopedPartnerId}`} replace />;
  }

  if (loading) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  if (!partner || !id) {
    return (
      <Card>
        <CardContent className="py-8 text-sm text-muted-foreground">{t("partnerNotFound" as never)}</CardContent>
      </Card>
    );
  }

  const defaultTab =
    partner.participationMode === "ReflectionOnly"
      ? "reflected"
      : partner.participationMode === "SubscriptionFee"
        ? "subscriptions"
        : "catalog";

  if (!tab) {
    return <Navigate to={`/partners/${id}/${defaultTab}`} replace />;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" asChild>
          <Link to="/partners">
            <ArrowLeft className="h-4 w-4" aria-hidden="true" />
            {t("navPartners" as never)}
          </Link>
        </Button>
      </div>

      <PageHeader
        title={partner.tradeName ?? partner.legalName}
        description={`${partnerTypeLabel(partner.type, lang)} · ${partner.participationMode} · ${partner.status}`}
        lang={lang}
      />

      <PartnerSubNav partner={partner} lang={lang} />
      <PartnerTabContent partner={partner} tab={tab} lang={lang} />
    </div>
  );
}

function PartnerTabContent({
  partner,
  tab,
  lang,
}: {
  partner: Partner;
  tab: string;
  lang: Lang;
}) {
  const t = useTranslator(lang);
  const [content, setContent] = useState<ReactNode>(null);
  const [loading, setLoading] = useState(true);
  const ds = getPortalDataSource();

  useEffect(() => {
    setLoading(true);
    void (async () => {
      switch (tab) {
        case "catalog":
        case "pricing": {
          const items = await ds.getCatalogItems(partner.id);
          setContent(
            <Card>
              <CardHeader>
                <CardTitle>{t("navCatalog" as never)}</CardTitle>
                <CardDescription>{t("catalogDesc" as never)}</CardDescription>
              </CardHeader>
              <CardContent>
                <ul className="divide-y divide-border">
                  {items.map((item) => (
                    <li key={item.id} className="flex flex-wrap gap-2 py-3 text-sm">
                      <span className="font-medium">{item.name}</span>
                      <span className="text-muted-foreground">({item.code})</span>
                      <span className="rounded bg-muted px-2 py-0.5 text-xs">{item.offeringKind}</span>
                      <span className="rounded bg-muted px-2 py-0.5 text-xs">{item.participationMode}</span>
                      <span className="ms-auto tabular-nums">{item.partnerCost.amount.toFixed(2)} SAR</span>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>,
          );
          break;
        }
        case "activations": {
          const acts = await ds.getActivations(partner.id);
          setContent(
            <div className="space-y-4">
              {acts.map(({ activation: a, workflow: wf }) => (
                <Card key={a.id}>
                  <CardHeader>
                    <CardTitle className="text-base">{a.merchantName}</CardTitle>
                    <CardDescription>
                      {a.catalogItemName} · {a.resalePrice.amount.toFixed(2)} SAR · {a.status}
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <ActivationWorkflow workflow={wf} lang={lang} />
                  </CardContent>
                </Card>
              ))}
            </div>,
          );
          break;
        }
        case "snapshots":
          setContent(
            <Card>
              <CardHeader>
                <CardTitle>{t("navSnapshots" as never)}</CardTitle>
                <CardDescription>{t("snapshotsDesc" as never)}</CardDescription>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">{t("mockSampleNotice" as never)}</p>
              </CardContent>
            </Card>,
          );
          break;
        case "settlement": {
          const cases = await ds.getSettlementCases(partner.id);
          setContent(
            <div className="space-y-4">
              {cases.map((c) => {
                const summary = deriveSettlementSummary(c.journal);
                return (
                  <Card key={c.id}>
                    <CardHeader>
                      <CardTitle className="text-base">{c.externalTransactionId}</CardTitle>
                      <CardDescription>
                        {c.state} · buy {summary.buyPrice.amount} → sell {summary.sellPrice.amount} SAR
                      </CardDescription>
                    </CardHeader>
                    <CardContent>
                      <JournalTable lines={c.journal.lines} caption={t("settlementJournalCaption" as never)} />
                    </CardContent>
                  </Card>
                );
              })}
            </div>,
          );
          break;
        }
        case "reversals": {
          const revs = await ds.getReversals(partner.id);
          setContent(
            <div className="space-y-4">
              {revs.map((r) => (
                <Card key={r.id}>
                  <CardHeader>
                    <CardTitle className="text-base">{r.id}</CardTitle>
                    <CardDescription>
                      {t("reversalOriginal" as never)}: {r.originalCaseId} ·{" "}
                      {r.netsToZero ? t("netsToZero" as never) : "—"}
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <JournalTable lines={r.journal.lines} />
                  </CardContent>
                </Card>
              ))}
            </div>,
          );
          break;
        }
        case "reflected": {
          const orders = await ds.getReflectedOrders(partner.id);
          setContent(
            <Card>
              <CardHeader>
                <CardTitle>{t("navReflectedOrders" as never)}</CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="divide-y divide-border text-sm">
                  {orders.map((o) => (
                    <li key={o.id} className="flex flex-wrap gap-2 py-3">
                      <span className="font-medium">{o.merchantName}</span>
                      <span className="text-muted-foreground">{o.externalTransactionId}</span>
                      <span className="rounded bg-muted px-2 py-0.5 text-xs">{o.posSyncStatus}</span>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>,
          );
          break;
        }
        case "pos-sync":
          setContent(
            <Card>
              <CardHeader>
                <CardTitle>{t("posSyncTitle" as never)}</CardTitle>
                <CardDescription>{t("posSyncDesc" as never)}</CardDescription>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">{t("mockSampleNotice" as never)}</p>
              </CardContent>
            </Card>,
          );
          break;
        case "subscriptions":
        case "billing-periods":
        case "invoices": {
          const periods = await ds.getBillingPeriods(partner.id);
          setContent(
            <Card>
              <CardHeader>
                <CardTitle>{t("navBilling" as never)}</CardTitle>
                <CardDescription>{t("billingDesc" as never)}</CardDescription>
              </CardHeader>
              <CardContent>
                <ul className="divide-y divide-border text-sm">
                  {periods.map((p) => (
                    <li key={p.id} className="flex flex-wrap gap-2 py-3">
                      <span className="font-medium">{p.periodKey}</span>
                      <span>{p.feeInclusive.amount.toFixed(2)} SAR</span>
                      <span className="text-muted-foreground">VAT {p.outputVat.toFixed(2)}</span>
                      <span className="text-muted-foreground">Net {p.netFee.toFixed(2)}</span>
                      {p.invoiceNumber ? (
                        <span className="rounded bg-muted px-2 py-0.5 text-xs">{p.invoiceNumber}</span>
                      ) : null}
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>,
          );
          break;
        }
        default:
          setContent(
            <Card>
              <CardContent className="py-8 text-sm text-muted-foreground">{t("sectionPlaceholder" as never)}</CardContent>
            </Card>,
          );
      }
      setLoading(false);
    })();
  }, [partner.id, tab, lang, t, ds]);

  if (loading) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  return <>{content}</>;
}
