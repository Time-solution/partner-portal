import { Fragment, useCallback, useEffect, useMemo, useState } from "react";
import { ChevronDown, ChevronRight, FileDown, FileSpreadsheet, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import {
  collectionCashFlow,
  deriveSettlementSummary,
  type CollectionRemittance,
  type SettlementCase,
} from "@/lib/data/types";
import { canDisburse } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { JournalTable } from "../components/JournalTable";
import { PageHeader } from "../components/PageHeader";
import { useTranslator, type Lang } from "@/lib/i18n";
import { settlementBookLabel, settlementStateLabel } from "@/lib/i18n/domainLabels";
import { paymentMethodLabel } from "@/lib/export/moneyExport";
import { formatOrderRef, formatTenantRef } from "@/lib/format/recordId";
import {
  downloadSettlementCsv,
  downloadSettlementXlsx,
  type SettlementExportInput,
} from "@/lib/export/settlementExport";
import type { ModuleScopeProps } from "../moduleScope";
import { filterByPartnerIds, useScopePartnerIds } from "../hooks/useScopePartnerIds";

const stateBadge: Record<string, string> = {
  Collected: "bg-sky-500/15 text-sky-700 dark:text-sky-300",
  Allocated: "bg-violet-500/15 text-violet-700 dark:text-violet-300",
  Invoiced: "bg-amber-500/15 text-amber-700 dark:text-amber-300",
  Cleared: "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300",
};

export function SettlementPage({ lang, moduleId, partnerId, financeMode }: ModuleScopeProps) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const { role, scopedPartnerId } = usePortalSession();
  const scopeIds = useScopePartnerIds(moduleId, partnerId ?? scopedPartnerId);
  const [cases, setCases] = useState<SettlementCase[]>([]);
  const [merchantByTenant, setMerchantByTenant] = useState<Map<string, string>>(new Map());
  const [reversedIds, setReversedIds] = useState<Set<string>>(new Set());
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [caseList, reversals, activations] = await Promise.all([
        ds.getSettlementCases(scopedPartnerId ?? partnerId),
        ds.getReversals(scopedPartnerId ?? partnerId),
        ds.getActivations(),
      ]);
      setCases(filterByPartnerIds(caseList, scopeIds));
      setReversedIds(new Set(reversals.map((r) => r.originalCaseId)));
      setMerchantByTenant(
        new Map(activations.map(({ activation: a }) => [a.tenantId, a.merchantName])),
      );
    } finally {
      setLoading(false);
    }
  }, [scopedPartnerId, partnerId, scopeIds?.join(",")]);

  useEffect(() => {
    void load();
  }, [load]);

  const showDisburse = canDisburse(role as never) && !financeMode;
  const canReverse = !financeMode && (role === "PlatformAdmin" || role === "Accountant");
  const showHeader = !moduleId && !partnerId && !financeMode;

  const merchantName = useCallback(
    (tenantId?: string) =>
      (tenantId && merchantByTenant.get(tenantId)) || formatTenantRef(tenantId ?? "", lang),
    [merchantByTenant, lang],
  );

  const exportInput: SettlementExportInput = useMemo(
    () => ({ cases, merchantName, lang }),
    [cases, merchantName, lang],
  );

  const triggerReversal = async (caseId: string) => {
    setBusyId(caseId);
    try {
      await getPortalDataSource().triggerReversal(caseId);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const toggle = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const doExportXlsx = async () => {
    setExporting(true);
    try {
      await downloadSettlementXlsx(exportInput);
    } finally {
      setExporting(false);
    }
  };

  const Caret = isRtl ? ChevronRight : ChevronDown;

  return (
    <div className="space-y-6">
      {showHeader ? (
        <PageHeader
          title={t("navSettlement" as never)}
          description={t("settlementDesc" as never)}
          lang={lang}
          showBeta
        />
      ) : null}

      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3">
          <CardTitle className="flex items-center gap-2 text-lg">
            {t("settlementCasesTitle" as never)}
            <span className="rounded-full bg-muted px-2 py-0.5 text-xs font-medium tabular-nums text-muted-foreground">
              {cases.length}
            </span>
          </CardTitle>
          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              variant="outline"
              disabled={loading || exporting || cases.length === 0}
              onClick={() => void doExportXlsx()}
            >
              {exporting ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <FileSpreadsheet className="h-4 w-4" />
              )}
              {t("exportExcel" as never)}
            </Button>
            <Button
              size="sm"
              variant="outline"
              disabled={loading || cases.length === 0}
              onClick={() => downloadSettlementCsv(exportInput)}
            >
              <FileDown className="h-4 w-4" />
              {t("exportCsv" as never)}
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-base text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : cases.length === 0 ? (
            <p className="py-8 text-center text-base text-muted-foreground">
              {t("settlementEmpty" as never)}
            </p>
          ) : (
            <div className="overflow-x-auto" dir={isRtl ? "rtl" : "ltr"}>
              <table className="w-full min-w-[920px] text-start text-base">
                <thead>
                  <tr className="border-b border-border text-sm text-muted-foreground">
                    <th className="px-3 py-2.5 text-start font-medium">{t("colOrder" as never)}</th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colPartner" as never)}</th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colMerchant" as never)}</th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colPaymentMethod" as never)}</th>
                    <th className="px-3 py-2.5 text-end font-medium">{t("settlementBuy" as never)}</th>
                    <th className="px-3 py-2.5 text-end font-medium">{t("settlementSell" as never)}</th>
                    <th className="px-3 py-2.5 text-end font-medium">{t("settlementMargin" as never)}</th>
                    <th className="px-3 py-2.5 text-end font-medium">
                      <span className="inline-flex items-center gap-1">
                        {t("settlementNetVat" as never)}
                        <BetaBadge lang={lang} />
                      </span>
                    </th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colStatus" as never)}</th>
                    <th className="px-3 py-2.5 text-end font-medium" aria-hidden="true" />
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/60">
                  {cases.map((c) => {
                    const s = deriveSettlementSummary(c.journal);
                    const isOpen = expanded.has(c.id);
                    const hasReversal = reversedIds.has(c.id);
                    return (
                      <Fragment key={c.id}>
                        <tr
                          className="cursor-pointer transition-colors hover:bg-muted/50"
                          onClick={() => toggle(c.id)}
                        >
                          <td className="px-3 py-3 text-start font-medium tabular-nums" title={c.externalTransactionId}>
                            {formatOrderRef(c.externalTransactionId, lang)}
                          </td>
                          <td className="px-3 py-3 text-start">{c.partnerName}</td>
                          <td className="px-3 py-3 text-start">{merchantName(c.tenantId)}</td>
                          <td className="px-3 py-3 text-start">
                            {c.collection ? (
                              <span
                                className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${
                                  c.collection.paymentMethod === "COD"
                                    ? "bg-orange-500/15 text-orange-700 dark:text-orange-300"
                                    : "bg-indigo-500/15 text-indigo-700 dark:text-indigo-300"
                                }`}
                              >
                                {paymentMethodLabel(lang, c.collection.paymentMethod)}
                              </span>
                            ) : (
                              <span className="text-muted-foreground">—</span>
                            )}
                          </td>
                          <td className="px-3 py-3 text-end tabular-nums">
                            <MoneyAmount amount={s.buyPrice.amount} />
                          </td>
                          <td className="px-3 py-3 text-end tabular-nums">
                            <MoneyAmount amount={s.sellPrice.amount} />
                          </td>
                          <td className="px-3 py-3 text-end tabular-nums font-medium text-emerald-700 dark:text-emerald-400">
                            <MoneyAmount amount={s.margin} />
                          </td>
                          <td className="px-3 py-3 text-end tabular-nums">
                            <MoneyAmount amount={s.netVatToZatca} />
                          </td>
                          <td className="px-3 py-3 text-start">
                            <span
                              className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${
                                stateBadge[c.state] ?? "bg-muted text-muted-foreground"
                              }`}
                            >
                              {settlementStateLabel(lang, c.state)}
                            </span>
                          </td>
                          <td className="px-3 py-3 text-end text-muted-foreground">
                            <Caret
                              className={`inline h-4 w-4 transition-transform ${isOpen ? "rotate-180" : ""}`}
                              aria-hidden="true"
                            />
                          </td>
                        </tr>
                        {isOpen ? (
                          <tr className="bg-muted/20">
                            <td colSpan={10} className="px-3 py-4">
                              <div className="space-y-4">
                                <div className="flex flex-wrap items-center gap-x-6 gap-y-1 text-sm text-muted-foreground">
                                  <span>{settlementBookLabel(lang, c.book)}</span>
                                  <span className="tabular-nums">
                                    {t("settlementVatOutput" as never)}: <MoneyAmount amount={s.outputVat} />
                                  </span>
                                  <span className="tabular-nums">
                                    {t("settlementVatInput" as never)}: <MoneyAmount amount={s.inputVat} />
                                  </span>
                                  <span className="tabular-nums">
                                    {t("settlementNetVat" as never)}: <MoneyAmount amount={s.netVatToZatca} />
                                  </span>
                                </div>
                                {c.collection ? (
                                  <CollectionPanel collection={c.collection} lang={lang} />
                                ) : null}
                                <JournalTable
                                  lang={lang}
                                  lines={c.journal.lines}
                                  caption={t("settlementJournalCaption" as never)}
                                />
                                {!financeMode && (canReverse || showDisburse) ? (
                                  <div
                                    className="flex flex-wrap items-center gap-2"
                                    onClick={(e) => e.stopPropagation()}
                                  >
                                    {canReverse && !hasReversal ? (
                                      <Button
                                        size="sm"
                                        variant="outline"
                                        disabled={busyId === c.id}
                                        onClick={() => void triggerReversal(c.id)}
                                      >
                                        {t("triggerReversal" as never)}
                                      </Button>
                                    ) : hasReversal ? (
                                      <span className="text-sm text-emerald-600">
                                        {t("reversalExists" as never)}
                                      </span>
                                    ) : null}
                                    {showDisburse ? (
                                      <Button size="sm" disabled title={t("disburseDisabledDemo" as never)}>
                                        {t("settlementDisburse" as never)}
                                      </Button>
                                    ) : null}
                                  </div>
                                ) : null}
                              </div>
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
    </div>
  );
}

function CollectionPanel({ collection, lang }: { collection: CollectionRemittance; lang: Lang }) {
  const t = useTranslator(lang);
  const c = collection;
  const isCod = c.paymentMethod === "COD";
  const inbound = collectionCashFlow(c) === "Inbound";

  const MoneyStat = ({ label, amount, accent }: { label: string; amount: number; accent?: string }) => (
    <div className="rounded-md border border-border bg-background px-3 py-2">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={`tabular-nums text-base font-semibold ${accent ?? ""}`}>
        <MoneyAmount amount={amount} />
      </div>
    </div>
  );

  const TextStat = ({ label, value }: { label: string; value: string }) => (
    <div className="rounded-md border border-border bg-background px-3 py-2">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="text-base font-semibold">{value}</div>
    </div>
  );

  return (
    <div className="rounded-lg border border-border bg-background/60 p-4">
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <h4 className="text-sm font-semibold">{t("collectionTitle" as never)}</h4>
        <span
          className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${
            isCod
              ? "bg-orange-500/15 text-orange-700 dark:text-orange-300"
              : "bg-indigo-500/15 text-indigo-700 dark:text-indigo-300"
          }`}
        >
          {paymentMethodLabel(lang, c.paymentMethod)}
        </span>
        <span className="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-muted-foreground">
          {inbound ? t("cashInbound" as never) : t("cashOutbound" as never)}
        </span>
      </div>

      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
        <TextStat label={t("collectedByLabel" as never)} value={t(`collectedBy_${c.collectedBy}` as never)} />
        <MoneyStat label={t("totalCollectedLabel" as never)} amount={c.totalCollected.amount} />
        <MoneyStat
          label={t("deductionsLabel" as never)}
          amount={c.deductions.reduce((s, d) => s + d.amount.amount, 0)}
        />
        <MoneyStat
          label={isCod ? t("netRemittedLabel" as never) : t("netPaidOutLabel" as never)}
          amount={c.netRemitted.amount}
          accent="text-emerald-700 dark:text-emerald-400"
        />
      </div>

      <div className="mt-3">
        <div className="mb-1.5 text-xs font-medium text-muted-foreground">{t("splitTitle" as never)}</div>
        <div className="grid gap-2 sm:grid-cols-3">
          <MoneyStat label={t("splitMerchant" as never)} amount={c.split.merchant.amount} />
          <MoneyStat label={t("splitDelivery" as never)} amount={c.split.deliveryCompany.amount} />
          <MoneyStat label={t("splitZahy" as never)} amount={c.split.zahy.amount} accent="text-primary" />
          <MoneyStat label={t("splitZatca" as never)} amount={c.split.zatca.amount} />
        </div>
      </div>

      {!isCod ? (
        <div className="mt-3 flex flex-wrap gap-x-6 gap-y-1 text-xs text-muted-foreground">
          <span>
            {t("gatewayRefLabel" as never)}: {c.gatewayReference || t("gatewayPending" as never)}
          </span>
          <span>
            {t("gatewayStatusLabel" as never)}: {c.gatewayStatus || t("gatewayPending" as never)}
          </span>
        </div>
      ) : null}
    </div>
  );
}
