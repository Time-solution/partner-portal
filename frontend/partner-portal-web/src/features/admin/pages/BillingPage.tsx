import { useCallback, useEffect, useMemo, useState } from "react";
import { ChevronDown, ChevronUp, Download, FileSpreadsheet, FileText, Loader2, Send } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { Riyal } from "@/components/Riyal";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import type { PaymentMethod, Receipt, SubscriptionBillingPeriod } from "@/lib/data/types";
import { BANK_PARENT_CODE, type BankAccount } from "@/lib/banks/bankAccount";
import { deriveInvoicePayment } from "@/lib/data/types";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { PageHeader } from "../components/PageHeader";
import { TableEmptyRow } from "../components/EmptyState";
import { useTranslator, type Lang } from "@/lib/i18n";
import { billingPeriodStatusLabel } from "@/lib/i18n/domainLabels";
import { paymentMethodLabel, paymentStatusLabel, downloadMoneyCsv, downloadMoneyXlsx } from "@/lib/export/moneyExport";
import { downloadReceiptPdf } from "@/lib/pdf/receiptPdf";
import { invoiceProformaFromPeriod } from "@/lib/invoice/proformaSources";
import { downloadProformaPdf } from "@/lib/invoice/proformaPdf";
import { downloadProformaXlsx } from "@/lib/invoice/proformaExcel";
import { filterByDate } from "@/lib/filters/dateRange";
import type { ModuleScopeProps } from "../moduleScope";
import { filterByPartnerIds, useScopePartnerIds } from "../hooks/useScopePartnerIds";
import { useDateRange } from "../hooks/useDateRange";
import { DateRangeFilter } from "../components/DateRangeFilter";

type BillingPageProps = ModuleScopeProps & {
  titleKey?: string;
  descKey?: string;
  invoicesOnly?: boolean;
};

const PAYMENT_CHIP: Record<string, string> = {
  Paid: "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border border-emerald-500/30",
  Partial: "bg-amber-500/15 text-amber-700 dark:text-amber-300 border border-amber-500/30",
  Overdue: "bg-rose-500/15 text-rose-700 dark:text-rose-300 border border-rose-500/30",
  Pending: "bg-slate-500/15 text-slate-700 dark:text-slate-300 border border-slate-500/30",
};

const PAY_METHODS: PaymentMethod[] = ["Transfer", "Online", "COD"];

function PaymentChip({ status, lang }: { status: string; lang: Lang }) {
  return (
    <span className={`inline-flex rounded-full px-2.5 py-0.5 text-sm font-medium ${PAYMENT_CHIP[status] ?? PAYMENT_CHIP.Pending}`}>
      {paymentStatusLabel(lang, status)}
    </span>
  );
}

export function BillingPage({
  lang,
  moduleId,
  partnerId,
  financeMode,
  titleKey = "navBilling",
  descKey = "billingDesc",
  invoicesOnly = false,
}: BillingPageProps) {
  const t = useTranslator(lang);
  const { scopedPartnerId, can } = usePortalSession();
  const scopeIds = useScopePartnerIds(moduleId, partnerId ?? scopedPartnerId);
  const { range, setRange } = useDateRange();
  const [periods, setPeriods] = useState<SubscriptionBillingPeriod[]>([]);
  const [receipts, setReceipts] = useState<Receipt[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const canManage = can(PortalPermissions.Billing.SetTerms) && !financeMode;
  // Recording a payment is an Accountant/Admin action — available in finance workspace too.
  const canRecordPayment = can(PortalPermissions.Billing.SetTerms);
  const defaultPartnerId = scopedPartnerId ?? "22222222-2222-2222-2222-222222222004";
  const showHeader = !moduleId && !partnerId && !financeMode;

  const showToast = (msg: string) => {
    setToast(msg);
    window.setTimeout(() => setToast(null), 3200);
  };

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [all, allReceipts] = await Promise.all([
        ds.getBillingPeriods(partnerId ?? scopedPartnerId),
        ds.getReceipts(partnerId ?? scopedPartnerId),
      ]);
      setPeriods(filterByPartnerIds(all, scopeIds));
      setReceipts(allReceipts);
    } finally {
      setLoading(false);
    }
  }, [scopedPartnerId, partnerId, scopeIds?.join(",")]);

  useEffect(() => {
    void load();
  }, [load]);

  const generateInvoice = async (periodId: string) => {
    setBusyId(periodId);
    try {
      await getPortalDataSource().generateInvoice(periodId);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const advancePeriod = async () => {
    setBusyId("advance");
    try {
      await getPortalDataSource().advanceBillingPeriod(defaultPartnerId);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  // Date filter layers ON TOP of partner scope — narrows billing rows by their period month.
  const dateScoped = filterByDate(periods, range, (p) => p.periodKey);
  const rows = invoicesOnly ? dateScoped.filter((p) => p.invoiceNumber || p.status === "Invoiced") : dateScoped;

  return (
    <div className="space-y-6">
      {showHeader ? (
        <PageHeader title={t(titleKey as never)} description={t(descKey as never)} lang={lang} showBeta />
      ) : null}
      <div className="flex flex-wrap items-end gap-2">
        <DateRangeFilter range={range} onChange={setRange} lang={lang} />
        {canManage ? (
          <Button variant="outline" disabled={busyId === "advance"} onClick={() => void advancePeriod()}>
            {t("billingAdvancePeriod" as never)}
          </Button>
        ) : null}
        <div className="ms-auto flex gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={rows.length === 0}
            onClick={() => downloadMoneyCsv({ invoices: rows, receipts, lang })}
          >
            <Download className="h-4 w-4" />
            {t("exportCsv" as never)}
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={rows.length === 0}
            onClick={() => void downloadMoneyXlsx({ invoices: rows, receipts, lang })}
          >
            <FileSpreadsheet className="h-4 w-4" />
            {t("exportExcel" as never)}
          </Button>
        </div>
      </div>
      <Card>
        <CardHeader>
          <CardTitle className="text-lg">
            {invoicesOnly ? t("invoicesListTitle" as never) : t("billingPeriodsTitle" as never)}
          </CardTitle>
          <CardDescription className="text-base">{t("billingVatOnFee" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-base text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[980px] text-base" dir={lang === "ar" ? "rtl" : "ltr"}>
                <thead>
                  <tr className="border-b border-border text-start text-muted-foreground">
                    <th className="px-2 py-2.5 text-start font-medium">{t("colPeriod" as never)}</th>
                    <th className="px-2 py-2.5 text-start font-medium">{t("colMerchant" as never)}</th>
                    <th className="px-2 py-2.5 text-end font-medium">{t("colFeeIncl" as never)}</th>
                    <th className="px-2 py-2.5 text-end font-medium">{t("colPaid" as never)}</th>
                    <th className="px-2 py-2.5 text-end font-medium">{t("colOutstanding" as never)}</th>
                    <th className="px-2 py-2.5 text-start font-medium">{t("colPayment" as never)}</th>
                    <th className="px-2 py-2.5 text-start font-medium">{t("colInvoice" as never)}</th>
                    <th className="px-2 py-2.5 text-start font-medium">{t("colStatus" as never)}</th>
                    <th className="px-2 py-2.5 text-end font-medium">{t("colActions" as never)}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.length === 0 ? (
                    <TableEmptyRow
                      colSpan={9}
                      message={t((invoicesOnly ? "invoicesEmpty" : "billingPeriodsEmpty") as never)}
                    />
                  ) : null}
                  {rows.map((p) => {
                    const state = deriveInvoicePayment(p);
                    const open = openId === p.id;
                    const invoiceReceipts = receipts.filter((r) => r.billingPeriodId === p.id);
                    return (
                      <BillingRow
                        key={p.id}
                        period={p}
                        lang={lang}
                        open={open}
                        invoiceReceipts={invoiceReceipts}
                        amountPaid={state.amountPaid}
                        amountOutstanding={state.amountOutstanding}
                        status={state.status}
                        canManage={canManage}
                        canRecordPayment={canRecordPayment}
                        busyId={busyId}
                        onToggle={() => setOpenId(open ? null : p.id)}
                        onGenerateInvoice={() => void generateInvoice(p.id)}
                        onRecordPayment={async (input) => {
                          setBusyId(p.id);
                          try {
                            await getPortalDataSource().recordPayment(p.id, input);
                            await load();
                            showToast(t("paymentRecordedToast" as never));
                          } finally {
                            setBusyId(null);
                          }
                        }}
                        onSendReceipt={async (receiptId) => {
                          await getPortalDataSource().sendReceipt(receiptId);
                          await load();
                          showToast(t("receiptSentToast" as never));
                        }}
                      />
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
      {toast ? (
        <div className="fixed inset-x-0 bottom-6 z-50 flex justify-center px-4">
          <div className="rounded-lg bg-foreground px-4 py-2.5 text-sm font-medium text-background shadow-lg">
            {toast}
          </div>
        </div>
      ) : null}
    </div>
  );
}

interface RecordPaymentDraft {
  amount: number;
  date: string;
  method: PaymentMethod;
  reference: string;
  /** Track B — bank the receipt landed in (BankAccount.id); absent → 1100 parent fallback. */
  bankAccountId?: string;
}

function BillingRow({
  period,
  lang,
  open,
  invoiceReceipts,
  amountPaid,
  amountOutstanding,
  status,
  canManage,
  canRecordPayment,
  busyId,
  onToggle,
  onGenerateInvoice,
  onRecordPayment,
  onSendReceipt,
}: {
  period: SubscriptionBillingPeriod;
  lang: Lang;
  open: boolean;
  invoiceReceipts: Receipt[];
  amountPaid: number;
  amountOutstanding: number;
  status: string;
  canManage: boolean;
  canRecordPayment: boolean;
  busyId: string | null;
  onToggle: () => void;
  onGenerateInvoice: () => void;
  onRecordPayment: (input: RecordPaymentDraft) => Promise<void>;
  onSendReceipt: (receiptId: string) => Promise<void>;
}) {
  const t = useTranslator(lang);
  const today = useMemo(() => new Date().toISOString().slice(0, 10), []);
  const [draft, setDraft] = useState<RecordPaymentDraft>({
    amount: amountOutstanding,
    date: today,
    method: "Transfer",
    reference: "",
    bankAccountId: undefined,
  });
  const [banks, setBanks] = useState<BankAccount[]>([]);
  useEffect(() => {
    if (!canRecordPayment) return;
    let active = true;
    void getPortalDataSource()
      .listBankAccounts()
      .then((list) => {
        if (active) setBanks(list.filter((b) => b.status === "Active"));
      });
    return () => {
      active = false;
    };
  }, [canRecordPayment]);
  const busy = busyId === period.id;

  return (
    <>
      <tr className="border-b border-border/60">
        <td className="px-2 py-2.5 font-medium">{period.periodKey}</td>
        <td className="px-2 py-2.5">{period.merchantName}</td>
        <td className="px-2 py-2.5 text-end tabular-nums">
          <MoneyAmount amount={period.feeInclusive.amount} />
        </td>
        <td className="px-2 py-2.5 text-end tabular-nums">
          <MoneyAmount amount={amountPaid} />
        </td>
        <td className="px-2 py-2.5 text-end tabular-nums font-medium">
          <MoneyAmount amount={amountOutstanding} />
        </td>
        <td className="px-2 py-2.5">
          <PaymentChip status={status} lang={lang} />
        </td>
        <td className="px-2 py-2.5 font-mono text-sm">
          {period.invoiceNumber ? (
            <span className="inline-flex items-center gap-1">
              {period.invoiceNumber}
              <BetaBadge lang={lang} />
            </span>
          ) : (
            "—"
          )}
        </td>
        <td className="px-2 py-2.5">
          <span className="rounded bg-muted px-2 py-0.5 text-sm">
            {billingPeriodStatusLabel(lang, period.status)}
          </span>
        </td>
        <td className="px-2 py-2.5">
          <div className="flex items-center justify-end gap-2">
            {canManage && period.status === "Charged" ? (
              <Button size="sm" variant="outline" disabled={busy} onClick={onGenerateInvoice}>
                {t("billingGenerateInvoice" as never)}
              </Button>
            ) : null}
            <Button size="sm" variant="ghost" onClick={onToggle} aria-expanded={open}>
              {open ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
            </Button>
          </div>
        </td>
      </tr>
      {open ? (
        <tr className="border-b border-border/60 bg-muted/30">
          <td colSpan={9} className="px-4 py-4">
            <div className="mb-4 flex flex-wrap items-center gap-2 border-b border-border/60 pb-3">
              <span className="inline-flex items-center gap-1.5 text-sm font-medium text-muted-foreground">
                {lang === "ar" ? "بيان مبدئي" : "Proforma statement"}
                <BetaBadge lang={lang} />
              </span>
              <div className="ms-auto flex gap-2">
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => void downloadProformaPdf(invoiceProformaFromPeriod(period, lang))}
                >
                  <FileText className="h-4 w-4" />
                  {lang === "ar" ? "تصدير PDF" : "Export PDF"}
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => void downloadProformaXlsx(invoiceProformaFromPeriod(period, lang))}
                >
                  <FileSpreadsheet className="h-4 w-4" />
                  {lang === "ar" ? "تصدير Excel" : "Export Excel"}
                </Button>
              </div>
            </div>
            <div className="grid gap-6 lg:grid-cols-2">
              {/* Payment history + receipts */}
              <div className="space-y-4">
                <div>
                  <h4 className="mb-2 text-sm font-semibold text-muted-foreground">{t("paymentHistory" as never)}</h4>
                  {period.payments && period.payments.length > 0 ? (
                    <ul className="space-y-1.5 text-sm">
                      {period.payments.map((pay) => (
                        <li key={pay.id} className="flex items-center justify-between gap-3 rounded bg-background px-3 py-1.5">
                          <span className="tabular-nums">{pay.date.slice(0, 10)}</span>
                          <span>{paymentMethodLabel(lang, pay.method)}</span>
                          <span className="font-mono text-xs text-muted-foreground">{pay.reference}</span>
                          <span className="tabular-nums font-medium">
                            <MoneyAmount amount={pay.amount.amount} />
                          </span>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p className="text-sm text-muted-foreground">{t("noPayments" as never)}</p>
                  )}
                </div>
                <div>
                  <h4 className="mb-2 text-sm font-semibold text-muted-foreground">
                    {t("receiptsTitle" as never)} <BetaBadge lang={lang} />
                  </h4>
                  {invoiceReceipts.length > 0 ? (
                    <ul className="space-y-1.5 text-sm">
                      {invoiceReceipts.map((r) => (
                        <li key={r.id} className="flex flex-wrap items-center justify-between gap-2 rounded bg-background px-3 py-2">
                          <span className="font-mono text-xs">{r.receiptNo}</span>
                          <span className="tabular-nums">
                            <MoneyAmount amount={r.amount.amount} />
                          </span>
                          <div className="flex items-center gap-1.5">
                            <span className="text-xs text-muted-foreground">{t("receiptPdfLabel" as never)}</span>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => void downloadReceiptPdf(r, "ar")}
                              title={t("receiptDownloadAr" as never)}
                            >
                              <Download className="h-3.5 w-3.5" />
                              {t("receiptLangAr" as never)}
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => void downloadReceiptPdf(r, "en")}
                              title={t("receiptDownloadEn" as never)}
                            >
                              <Download className="h-3.5 w-3.5" />
                              {t("receiptLangEn" as never)}
                            </Button>
                            {r.sent ? (
                              <span className="rounded-full bg-emerald-500/15 px-2 py-0.5 text-xs font-medium text-emerald-700 dark:text-emerald-300">
                                {t("receiptSent" as never)} ✓
                              </span>
                            ) : (
                              <Button size="sm" variant="ghost" onClick={() => void onSendReceipt(r.id)}>
                                <Send className="h-3.5 w-3.5" />
                                {t("sendReceipt" as never)}
                              </Button>
                            )}
                          </div>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p className="text-sm text-muted-foreground">—</p>
                  )}
                </div>
              </div>

              {/* Record payment form */}
              {canRecordPayment ? (
                <div className="rounded-lg border border-border bg-background p-4">
                  <h4 className="mb-3 text-sm font-semibold">{t("recordPaymentTitle" as never)}</h4>
                  <form
                    className="grid gap-3 sm:grid-cols-2"
                    onSubmit={(e) => {
                      e.preventDefault();
                      if (!(draft.amount > 0) || busy) return;
                      void onRecordPayment(draft).then(() =>
                        setDraft({ amount: 0, date: today, method: "Transfer", reference: "" }),
                      );
                    }}
                  >
                    <label className="text-sm">
                      <span className="mb-1 inline-flex items-baseline gap-1 text-muted-foreground">
                        {t("paymentAmount" as never)} (<Riyal />)
                      </span>
                      <input
                        type="number"
                        step="0.01"
                        min="0"
                        value={draft.amount || ""}
                        onChange={(e) => setDraft((d) => ({ ...d, amount: Number(e.target.value) }))}
                        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                      />
                    </label>
                    <label className="text-sm">
                      <span className="mb-1 block text-muted-foreground">{t("paymentDate" as never)}</span>
                      <input
                        type="date"
                        value={draft.date}
                        onChange={(e) => setDraft((d) => ({ ...d, date: e.target.value }))}
                        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                      />
                    </label>
                    <label className="text-sm">
                      <span className="mb-1 block text-muted-foreground">{t("paymentMethod" as never)}</span>
                      <select
                        value={draft.method}
                        onChange={(e) => setDraft((d) => ({ ...d, method: e.target.value as PaymentMethod }))}
                        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                      >
                        {PAY_METHODS.map((m) => (
                          <option key={m} value={m}>
                            {paymentMethodLabel(lang, m)}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="text-sm">
                      <span className="mb-1 block text-muted-foreground">{t("paymentReference" as never)}</span>
                      <input
                        type="text"
                        value={draft.reference}
                        onChange={(e) => setDraft((d) => ({ ...d, reference: e.target.value }))}
                        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                      />
                    </label>
                    <label className="text-sm">
                      <span className="mb-1 block text-muted-foreground">{t("paymentBank" as never)}</span>
                      <select
                        value={draft.bankAccountId ?? ""}
                        onChange={(e) =>
                          setDraft((d) => ({ ...d, bankAccountId: e.target.value || undefined }))
                        }
                        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                      >
                        <option value="">{t("paymentBankFallback" as never)}</option>
                        {banks.map((b) => (
                          <option key={b.id} value={b.id}>
                            {b.code} · {b.name}
                          </option>
                        ))}
                      </select>
                    </label>
                    <p className="text-xs text-muted-foreground sm:col-span-2">
                      {t("paymentBankRoutingNote" as never)}{" "}
                      <span className="font-mono">
                        {banks.find((b) => b.id === draft.bankAccountId)?.code ?? BANK_PARENT_CODE}
                      </span>
                    </p>
                    <div className="sm:col-span-2">
                      <Button type="submit" size="sm" disabled={busy || !(draft.amount > 0)}>
                        {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                        {t("paymentSave" as never)}
                      </Button>
                    </div>
                  </form>
                </div>
              ) : null}
            </div>
          </td>
        </tr>
      ) : null}
    </>
  );
}
