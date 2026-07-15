import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { StatusBadge } from "@/components/catalog/badges";
import { CardGridSkeleton, EmptyState, ErrorState } from "@/components/states";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { useTranslator, type Lang } from "@/lib/i18n";
import type { CreateManualInvoiceInput, ManualInvoice } from "@/lib/data/types";
import { downloadManualInvoicePdf } from "@/lib/finance/manualInvoiceApi";
import { ManualInvoiceForm, type ManualRecipientOption } from "./ManualInvoiceForm";

export { ManualInvoiceForm } from "./ManualInvoiceForm";
export type { ManualRecipientOption } from "./ManualInvoiceForm";

/** Route page — the keyed-invoice form + the P4 lifecycle list (Draft → Issue with confirm). */
export function ManualInvoicePage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { can } = usePortalSession();
  const canWrite = can(PortalPermissions.Finance.WriteManualInvoice);

  const [recipients, setRecipients] = useState<ManualRecipientOption[]>([]);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<ManualInvoice | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [invoices, setInvoices] = useState<ManualInvoice[]>([]);
  const [listLoading, setListLoading] = useState(true);
  const [listError, setListError] = useState<string | null>(null);
  const [confirmIssueId, setConfirmIssueId] = useState<string | null>(null);
  const [issueBusy, setIssueBusy] = useState(false);

  const loadInvoices = useCallback(async () => {
    setListLoading(true);
    setListError(null);
    try {
      setInvoices(await getPortalDataSource().listInvoices({ source: "Manual" }));
    } catch (e) {
      setListError(e instanceof Error ? e.message : String(e));
    } finally {
      setListLoading(false);
    }
  }, []);

  useEffect(() => {
    void getPortalDataSource()
      .getPartners()
      .then((partners) =>
        setRecipients(
          partners.map((p) => ({
            id: p.id,
            name: p.tradeName || p.legalName,
            type: "Partner" as const,
            // Mock KYC readiness: an Active partner is treated as KYC-complete.
            kycComplete: p.status === "Active",
          })),
        ),
      );
    void loadInvoices();
  }, [loadInvoices]);

  const handleSubmit = async (input: CreateManualInvoiceInput) => {
    setBusy(true);
    setError(null);
    try {
      setResult(await getPortalDataSource().createManualInvoice(input));
      await loadInvoices();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const handleDownload = async (invoice: ManualInvoice) => {
    try {
      await downloadManualInvoicePdf(invoice, lang);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const handleIssue = async (id: string) => {
    setIssueBusy(true);
    try {
      await getPortalDataSource().issueManualInvoice(id);
      setConfirmIssueId(null);
      await loadInvoices();
    } catch (e) {
      setListError(e instanceof Error ? e.message : String(e));
      setConfirmIssueId(null);
    } finally {
      setIssueBusy(false);
    }
  };

  return (
    <div className="space-y-6" dir={lang === "ar" ? "rtl" : "ltr"}>
      <ManualInvoiceForm
        lang={lang}
        canWrite={canWrite}
        recipients={recipients}
        busy={busy}
        result={result}
        error={error}
        onSubmit={handleSubmit}
        onDownloadPdf={handleDownload}
        onReset={() => setResult(null)}
      />

      <Card data-testid="manual-invoice-list">
        <CardHeader className="pb-3">
          <div className="flex flex-wrap items-center gap-2">
            <CardTitle className="text-base">{t("manualInvListTitle" as never)}</CardTitle>
            <BetaBadge lang={lang} />
          </div>
          <CardDescription>{t("manualInvListDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {listLoading ? (
            <CardGridSkeleton count={2} />
          ) : listError ? (
            <ErrorState
              message={t("stateErrorGeneric" as never)}
              retryLabel={t("stateRetry" as never)}
              onRetry={() => void loadInvoices()}
            />
          ) : invoices.length === 0 ? (
            <EmptyState message={t("manualInvListEmpty" as never)} />
          ) : (
            invoices.map((invoice) => (
              <div
                key={invoice.id}
                data-testid={`manual-invoice-${invoice.id}`}
                data-status={invoice.status}
                className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border p-3"
              >
                <div className="min-w-0">
                  <p className="font-mono text-sm font-medium">{invoice.invoiceNumber}</p>
                  <p className="truncate text-xs text-muted-foreground">
                    {invoice.recipient} ·{" "}
                    {new Date(invoice.issueDate).toLocaleDateString(lang === "ar" ? "ar-SA" : "en-GB")}
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-sm font-semibold tabular-nums">
                    <MoneyAmount amount={invoice.grandTotalInclusive} />
                  </span>
                  <StatusBadge
                    status={invoice.status}
                    label={t(
                      (invoice.status === "Draft" ? "manualInvStatusDraft" : "manualInvStatusIssued") as never,
                    )}
                  />
                  {canWrite && invoice.status === "Draft" ? (
                    <Button
                      size="sm"
                      data-testid={`issue-${invoice.id}`}
                      disabled={issueBusy}
                      onClick={() => setConfirmIssueId(invoice.id)}
                    >
                      {t("manualInvIssueAction" as never)}
                    </Button>
                  ) : null}
                  <Button size="sm" variant="outline" onClick={() => void handleDownload(invoice)}>
                    PDF
                  </Button>
                </div>
              </div>
            ))
          )}
        </CardContent>
      </Card>

      {confirmIssueId ? (
        <ConfirmDialog
          open
          dir={lang === "ar" ? "rtl" : "ltr"}
          testId="issue-confirm"
          title={t("manualInvIssueConfirmTitle" as never)}
          confirmLabel={t("manualInvIssueAction" as never)}
          cancelLabel={t("confirmCancel" as never)}
          busy={issueBusy}
          onCancel={() => setConfirmIssueId(null)}
          onConfirm={() => void handleIssue(confirmIssueId)}
        >
          <p>{t("manualInvIssueConfirmBody" as never)}</p>
        </ConfirmDialog>
      ) : null}
    </div>
  );
}
