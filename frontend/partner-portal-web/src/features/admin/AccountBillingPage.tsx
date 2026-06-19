import { useCallback, useEffect, useMemo, useState } from "react";
import { Download, FileText, Loader2, RefreshCw, Wallet } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  downloadFinanceDocument,
  exportFinancePostings,
  fetchFinanceAccount,
  fetchFinanceDocuments,
  fetchFinancePostings,
  type FinanceAccountStatus,
  type FinanceDocumentKind,
  type FinanceDocumentListItem,
  type FinancePortalAccount,
  type FinancePortalPostingRow,
  type KycVerificationStatus,
} from "@/lib/financeApi";
import { useTranslator, type Lang } from "@/lib/i18n";

const PAGE_SIZE = 20;

const ACCOUNT_STATUS: Record<number, FinanceAccountStatus> = {
  1: "Pending",
  2: "Active",
  3: "Suspended",
  4: "Closed",
};

const KYC_STATUS: Record<number, KycVerificationStatus> = {
  1: "Submitted",
  2: "UnderReview",
  3: "Verified",
  4: "Rejected",
};

const DOCUMENT_KIND: Record<number, FinanceDocumentKind> = {
  1: "Statement",
  2: "Invoice",
};

function normalizeAccountStatus(raw: FinanceAccountStatus | number): FinanceAccountStatus {
  return typeof raw === "number" ? (ACCOUNT_STATUS[raw] ?? "Pending") : raw;
}

function normalizeKycStatus(raw: KycVerificationStatus | number): KycVerificationStatus {
  return typeof raw === "number" ? (KYC_STATUS[raw] ?? "Submitted") : raw;
}

function normalizeDocumentKind(raw: FinanceDocumentKind | number): FinanceDocumentKind {
  return typeof raw === "number" ? (DOCUMENT_KIND[raw] ?? "Invoice") : raw;
}

function formatMoney(amount: number, currency: string, lang: Lang) {
  return new Intl.NumberFormat(lang === "ar" ? "ar-SA" : "en-SA", {
    style: "currency",
    currency,
    minimumFractionDigits: 2,
  }).format(amount);
}

function formatDate(value: string, lang: Lang) {
  return new Intl.DateTimeFormat(lang === "ar" ? "ar-SA" : "en-SA", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function triggerBrowserDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

interface AccountBillingPageProps {
  lang: Lang;
}

export function AccountBillingPage({ lang }: AccountBillingPageProps) {
  const t = useTranslator(lang);
  const [account, setAccount] = useState<FinancePortalAccount | null>(null);
  const [postings, setPostings] = useState<FinancePortalPostingRow[]>([]);
  const [postingsTotal, setPostingsTotal] = useState(0);
  const [documents, setDocuments] = useState<FinanceDocumentListItem[]>([]);
  const [page, setPage] = useState(0);
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [loading, setLoading] = useState(true);
  const [postingsLoading, setPostingsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [exporting, setExporting] = useState<"csv" | "xlsx" | null>(null);

  const currency = account?.currency ?? "SAR";

  const statusLabel = useCallback(
    (status: FinanceAccountStatus | number) => {
      const normalized = normalizeAccountStatus(status);
      const map: Record<FinanceAccountStatus, string> = {
        Pending: t("billingStatusPending" as never),
        Active: t("billingStatusActive" as never),
        Suspended: t("billingStatusSuspended" as never),
        Closed: t("billingStatusClosed" as never),
      };
      return map[normalized];
    },
    [t],
  );

  const kycLabel = useCallback(
    (status: KycVerificationStatus | number) => {
      const normalized = normalizeKycStatus(status);
      const map: Record<KycVerificationStatus, string> = {
        Submitted: t("billingKycSubmitted" as never),
        UnderReview: t("billingKycUnderReview" as never),
        Verified: t("billingKycVerified" as never),
        Rejected: t("billingKycRejected" as never),
      };
      return map[normalized];
    },
    [t],
  );

  const documentKindLabel = useCallback(
    (kind: FinanceDocumentKind | number) => {
      const normalized = normalizeDocumentKind(kind);
      return normalized === "Statement"
        ? t("billingDocStatement" as never)
        : t("billingDocInvoice" as never);
    },
    [t],
  );

  const loadAccountAndDocuments = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [accountResult, documentsResult] = await Promise.all([
        fetchFinanceAccount(),
        fetchFinanceDocuments(),
      ]);
      setAccount(accountResult);
      setDocuments(documentsResult);
    } catch {
      setError(t("billingLoadError" as never));
    } finally {
      setLoading(false);
    }
  }, [t]);

  const loadPostings = useCallback(async () => {
    setPostingsLoading(true);
    try {
      const result = await fetchFinancePostings({
        skipCount: page * PAGE_SIZE,
        maxResultCount: PAGE_SIZE,
        from: fromDate || undefined,
        to: toDate || undefined,
      });
      setPostings(result.items);
      setPostingsTotal(result.totalCount);
    } catch {
      setError(t("billingLoadError" as never));
    } finally {
      setPostingsLoading(false);
    }
  }, [fromDate, page, t, toDate]);

  useEffect(() => {
    void loadAccountAndDocuments();
  }, [loadAccountAndDocuments]);

  useEffect(() => {
    void loadPostings();
  }, [loadPostings]);

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil(postingsTotal / PAGE_SIZE)),
    [postingsTotal],
  );

  async function handleExport(format: "csv" | "xlsx") {
    setExporting(format);
    setError(null);
    try {
      const file = await exportFinancePostings({
        format,
        from: fromDate || undefined,
        to: toDate || undefined,
      });
      triggerBrowserDownload(file.blob, file.fileName);
    } catch {
      setError(t("billingDownloadError" as never));
    } finally {
      setExporting(null);
    }
  }

  async function handleDocumentDownload(documentId: string) {
    setDownloadingId(documentId);
    setError(null);
    try {
      const file = await downloadFinanceDocument(documentId);
      triggerBrowserDownload(file.blob, file.fileName);
    } catch {
      setError(t("billingDownloadError" as never));
    } finally {
      setDownloadingId(null);
    }
  }

  if (loading && !account) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
        {t("billingLoading" as never)}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm text-muted-foreground">{t("billingDescription" as never)}</p>
          <p className="mt-1 text-xs text-muted-foreground">{t("billingReadOnlyNotice" as never)}</p>
        </div>
        <Button variant="outline" size="sm" onClick={() => void loadAccountAndDocuments()}>
          <RefreshCw className="h-4 w-4" aria-hidden="true" />
          {t("billingRefresh" as never)}
        </Button>
      </div>

      {error ? (
        <p className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      ) : null}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>{t("billingBalance" as never)}</CardDescription>
            <CardTitle className="flex items-center gap-2 text-2xl">
              <Wallet className="h-5 w-5 text-primary" aria-hidden="true" />
              {account ? formatMoney(account.balance, currency, lang) : "—"}
            </CardTitle>
          </CardHeader>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardDescription>{t("billingAccountStatus" as never)}</CardDescription>
            <CardTitle className="text-lg">
              {account ? statusLabel(account.status) : "—"}
            </CardTitle>
          </CardHeader>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardDescription>{t("billingKycStatus" as never)}</CardDescription>
            <CardTitle className="text-lg">
              {account ? kycLabel(account.kycStatus) : "—"}
            </CardTitle>
          </CardHeader>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardDescription>{t("billingOperational" as never)}</CardDescription>
            <CardTitle className="text-lg">
              {account
                ? account.isOperational
                  ? t("billingOperationalYes" as never)
                  : t("billingOperationalNo" as never)
                : "—"}
            </CardTitle>
          </CardHeader>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t("billingPostingsTitle" as never)}</CardTitle>
          <CardDescription>{t("billingPostingsDescription" as never)}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-1">
              <Label htmlFor="billing-from">{t("billingFromDate" as never)}</Label>
              <Input
                id="billing-from"
                type="date"
                value={fromDate}
                onChange={(event) => {
                  setPage(0);
                  setFromDate(event.target.value);
                }}
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="billing-to">{t("billingToDate" as never)}</Label>
              <Input
                id="billing-to"
                type="date"
                value={toDate}
                onChange={(event) => {
                  setPage(0);
                  setToDate(event.target.value);
                }}
              />
            </div>
            <Button variant="outline" size="sm" disabled={!!exporting} onClick={() => void handleExport("csv")}>
              {exporting === "csv" ? (
                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
              ) : (
                <Download className="h-4 w-4" aria-hidden="true" />
              )}
              {t("billingExportCsv" as never)}
            </Button>
            <Button variant="outline" size="sm" disabled={!!exporting} onClick={() => void handleExport("xlsx")}>
              {exporting === "xlsx" ? (
                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
              ) : (
                <Download className="h-4 w-4" aria-hidden="true" />
              )}
              {t("billingExportXlsx" as never)}
            </Button>
          </div>

          <div className="overflow-x-auto rounded-md border border-border">
            <table className="w-full min-w-[640px] text-sm">
              <thead className="bg-muted/50 text-muted-foreground">
                <tr>
                  <th className="px-3 py-2 text-start font-medium">{t("billingColDate" as never)}</th>
                  <th className="px-3 py-2 text-start font-medium">{t("billingColDescription" as never)}</th>
                  <th className="px-3 py-2 text-end font-medium">{t("billingColAmount" as never)}</th>
                  <th className="px-3 py-2 text-end font-medium">{t("billingColBalance" as never)}</th>
                </tr>
              </thead>
              <tbody>
                {postingsLoading ? (
                  <tr>
                    <td colSpan={4} className="px-3 py-6 text-center text-muted-foreground">
                      <Loader2 className="mx-auto h-4 w-4 animate-spin" aria-hidden="true" />
                    </td>
                  </tr>
                ) : postings.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="px-3 py-6 text-center text-muted-foreground">
                      {t("billingPostingsEmpty" as never)}
                    </td>
                  </tr>
                ) : (
                  postings.map((row) => (
                    <tr key={row.postingId} className="border-t border-border">
                      <td className="px-3 py-2 whitespace-nowrap">{formatDate(row.postedAt, lang)}</td>
                      <td className="px-3 py-2">{row.description ?? row.sourceType}</td>
                      <td className="px-3 py-2 text-end tabular-nums">
                        {formatMoney(row.postingAmount, currency, lang)}
                      </td>
                      <td className="px-3 py-2 text-end tabular-nums">
                        {formatMoney(row.runningBalance, currency, lang)}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="flex items-center justify-between gap-2 text-sm">
            <span className="text-muted-foreground">
              {t("billingPostingsSummary" as never).replace("{count}", String(postingsTotal))}
            </span>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page === 0 || postingsLoading}
                onClick={() => setPage((current) => Math.max(0, current - 1))}
              >
                {t("billingPrevPage" as never)}
              </Button>
              <span className="text-muted-foreground">
                {page + 1} / {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={page + 1 >= totalPages || postingsLoading}
                onClick={() => setPage((current) => current + 1)}
              >
                {t("billingNextPage" as never)}
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("billingDocumentsTitle" as never)}</CardTitle>
          <CardDescription>{t("billingDocumentsDescription" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          {documents.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("billingDocumentsEmpty" as never)}</p>
          ) : (
            <ul className="divide-y divide-border rounded-md border border-border">
              {documents.map((doc) => (
                <li key={doc.documentId} className="flex flex-wrap items-center justify-between gap-3 px-3 py-3">
                  <div className="min-w-0">
                    <p className="flex items-center gap-2 font-medium">
                      <FileText className="h-4 w-4 text-primary" aria-hidden="true" />
                      {doc.invoiceNumber || documentKindLabel(doc.documentKind)}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {documentKindLabel(doc.documentKind)} · {formatDate(doc.generatedAt, lang)} ·{" "}
                      {formatMoney(doc.postingSum, currency, lang)}
                    </p>
                  </div>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={downloadingId === doc.documentId}
                    onClick={() => void handleDocumentDownload(doc.documentId)}
                  >
                    {downloadingId === doc.documentId ? (
                      <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    ) : (
                      <Download className="h-4 w-4" aria-hidden="true" />
                    )}
                    {t("billingDownload" as never)}
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
