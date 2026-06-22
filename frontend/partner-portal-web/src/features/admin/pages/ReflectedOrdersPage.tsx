import { Fragment, useEffect, useMemo, useState } from "react";
import { ChevronDown, ChevronRight, Loader2 } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ReflectionDetailPanel } from "@/features/admin/components/ReflectionDetailPanel";
import { getPortalDataSource } from "@/lib/data";
import type { ReflectedPartnerOrder } from "@/lib/data/types";
import { PageHeader } from "../components/PageHeader";
import { TableEmptyRow } from "../components/EmptyState";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { posSyncStatusLabel } from "@/lib/i18n/domainLabels";
import { useTranslator, type Lang } from "@/lib/i18n";
import { formatOrderRef } from "@/lib/format/recordId";
import { buildPartnerBulkInvoice, type BulkInvoice } from "@/lib/reports/activationFees";
import { periodOf } from "@/lib/reports/settlementReports";
import { filterByDate } from "@/lib/filters/dateRange";
import type { ModuleScopeProps } from "../moduleScope";
import { filterByPartnerIds, useScopePartnerIds } from "../hooks/useScopePartnerIds";
import { useDateRange } from "../hooks/useDateRange";
import { DateRangeFilter } from "../components/DateRangeFilter";

export function ReflectedOrdersPage({ lang, moduleId, partnerId, financeMode }: ModuleScopeProps) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const scopeIds = useScopePartnerIds(moduleId, partnerId);
  const { range, setRange } = useDateRange();
  const [allOrders, setAllOrders] = useState<ReflectedPartnerOrder[]>([]);
  const [loading, setLoading] = useState(true);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const showHeader = !moduleId && !partnerId && !financeMode;
  const Caret = isRtl ? ChevronRight : ChevronDown;

  // Date filter layers ON TOP of org/partner scope — narrows by the order's reflected date.
  const orders = useMemo(
    () => filterByDate(allOrders, range, (o) => o.reflectedAt),
    [allOrders, range],
  );

  useEffect(() => {
    void getPortalDataSource()
      .getReflectedOrders(partnerId)
      .then((all) => filterByPartnerIds(all, scopeIds))
      .then(setAllOrders)
      .finally(() => setLoading(false));
  }, [partnerId, scopeIds?.join(",")]);

  const toggle = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  // Per-partner bulk invoices for the latest period that has successful txns.
  const bulkInvoices = useMemo<BulkInvoice[]>(() => {
    const successful = orders.filter((o) => o.reflection?.successful);
    const periods = Array.from(new Set(successful.map((o) => periodOf(o.reflectedAt)))).sort().reverse();
    const latest = periods[0];
    if (!latest) return [];
    const partnerIds = Array.from(new Set(successful.filter((o) => periodOf(o.reflectedAt) === latest).map((o) => o.partnerId)));
    return partnerIds
      .map((pid) => buildPartnerBulkInvoice(orders, pid, latest))
      .filter((inv) => inv.transactionCount > 0);
  }, [orders]);

  return (
    <div className="space-y-6">
      {showHeader ? (
        <PageHeader title={t("navReflectedOrders" as never)} description={t("reflectedDesc" as never)} lang={lang} />
      ) : null}

      {bulkInvoices.map((inv) => (
        <BulkInvoiceCard key={inv.partnerId} invoice={inv} lang={lang} />
      ))}

      <Card>
        <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle>{t("reflectedListTitle" as never)}</CardTitle>
            <CardDescription>{t("reflectedNoMoney" as never)}</CardDescription>
          </div>
          <DateRangeFilter range={range} onChange={setRange} lang={lang} />
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : (
            <div className="overflow-x-auto" dir={isRtl ? "rtl" : "ltr"}>
              <table className="w-full min-w-[720px] text-start text-base">
                <thead>
                  <tr className="border-b border-border text-sm text-muted-foreground">
                    <th className="w-8 px-2 py-2.5" />
                    <th className="px-3 py-2.5 text-start font-medium">{t("colPartner" as never)}</th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colMerchant" as never)}</th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colOrder" as never)}</th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colPosSync" as never)}</th>
                    <th className="px-3 py-2.5 text-start font-medium">{t("colReflectedAt" as never)}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/60">
                  {orders.length === 0 ? (
                    <TableEmptyRow colSpan={6} message={t("reflectedEmpty" as never)} />
                  ) : null}
                  {orders.map((o) => {
                    const open = expanded.has(o.id);
                    return (
                      <Fragment key={o.id}>
                        <tr
                          className={o.reflection ? "cursor-pointer hover:bg-muted/50" : ""}
                          onClick={() => o.reflection && toggle(o.id)}
                        >
                          <td className="px-2 py-2.5 text-muted-foreground">
                            {o.reflection ? <Caret className="h-4 w-4" /> : null}
                          </td>
                          <td className="px-3 py-2.5 text-start font-medium">{o.partnerName}</td>
                          <td className="px-3 py-2.5 text-start">{o.merchantName}</td>
                          <td className="px-3 py-2.5 text-start tabular-nums" title={o.externalTransactionId}>
                            {formatOrderRef(o.externalTransactionId, lang)}
                          </td>
                          <td className="px-3 py-2.5 text-start">
                            <span className="inline-flex rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium">
                              {posSyncStatusLabel(lang, o.posSyncStatus)}
                            </span>
                          </td>
                          <td className="px-3 py-2.5 text-start text-sm text-muted-foreground">
                            {new Date(o.reflectedAt).toLocaleString(lang === "ar" ? "ar-SA" : "en-GB")}
                          </td>
                        </tr>
                        {open && o.reflection ? (
                          <tr>
                            <td colSpan={6} className="bg-muted/30 px-3 py-3">
                              <ReflectionDetailPanel order={o} lang={lang} viewer="full" />
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

function BulkInvoiceCard({ invoice, lang }: { invoice: BulkInvoice; lang: Lang }) {
  const t = useTranslator(lang);
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-lg">
          {invoice.partnerName} · {invoice.period}
          <BetaBadge lang={lang} />
        </CardTitle>
        <CardDescription>{t("bulkInvoiceDesc" as never)}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-4">
          <Stat label={t("bulkInvoiceTxns" as never)} value={String(invoice.transactionCount)} />
          <Stat label={t("bulkInvoiceTotal" as never)} value={invoice.totalInclusive} />
          <Stat label={t("bulkInvoiceNet" as never)} value={invoice.totalNet} />
          <Stat label={t("bulkInvoiceVat" as never)} value={invoice.totalVat} />
        </div>
        <div className="space-y-2">
          {invoice.lines.map((line) => (
            <details key={line.tenantId} className="rounded-md border border-border">
              <summary className="flex cursor-pointer items-center justify-between gap-2 px-3 py-2 text-sm font-medium">
                <span>{line.merchantName}</span>
                <span className="tabular-nums">
                  {line.transactions.length} × · <MoneyAmount amount={line.subtotalInclusive} />
                </span>
              </summary>
              <ul className="divide-y divide-border/60 px-3 pb-2 text-sm">
                {line.transactions.map((tx) => (
                  <li key={tx.orderRef} className="flex items-center justify-between gap-2 py-1.5">
                    <span className="tabular-nums" title={tx.orderRef}>
                      {formatOrderRef(tx.orderRef, lang)}
                    </span>
                    <span className="tabular-nums text-muted-foreground">
                      <MoneyAmount amount={tx.feeInclusive} />
                    </span>
                  </li>
                ))}
              </ul>
            </details>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

function Stat({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-md border border-border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-lg font-bold tabular-nums">
        {typeof value === "number" ? <MoneyAmount amount={value} /> : value}
      </p>
    </div>
  );
}
