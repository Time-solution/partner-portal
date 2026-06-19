import { userManager } from "./auth/userManager";
import { apiBaseUrl } from "./auth/oidcConfig";
import type { PagedResult } from "./api";

export type FinanceAccountStatus = "Pending" | "Active" | "Suspended" | "Closed";
export type KycVerificationStatus = "Submitted" | "UnderReview" | "Verified" | "Rejected";
export type FinanceDocumentKind = "Statement" | "Invoice";

export interface FinancePortalAccount {
  accountId: string;
  accountKind: string;
  balance: number;
  currency: string;
  status: FinanceAccountStatus | number;
  kycStatus: KycVerificationStatus | number;
  isOperational: boolean;
}

export interface FinancePortalPostingRow {
  postingId: string;
  postedAt: string;
  postingAmount: number;
  runningBalance: number;
  sourceModule: string | number;
  sourceType: string;
  sourceId: string;
  description?: string;
}

export interface FinanceDocumentListItem {
  documentId: string;
  documentKind: FinanceDocumentKind | number;
  invoiceNumber: string;
  postingSum: number;
  generatedAt: string;
}

export interface FinancePostingsParams {
  skipCount?: number;
  maxResultCount?: number;
  from?: string;
  to?: string;
}

export interface FinanceExportParams {
  format: "csv" | "xlsx";
  from?: string;
  to?: string;
}

export interface DownloadedFile {
  blob: Blob;
  fileName: string;
}

async function getAccessToken(): Promise<string | null> {
  const user = await userManager.getUser();
  if (!user || user.expired) {
    return null;
  }
  return user.access_token;
}

async function financeFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = await getAccessToken();
  if (!token) {
    throw new Error("NotAuthenticated");
  }

  const headers = new Headers(init.headers);
  headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers,
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Request failed (${response.status})`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

function parseContentDispositionFileName(header: string | null): string | null {
  if (!header) return null;
  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1]);
  }
  const plainMatch = /filename="?([^";]+)"?/i.exec(header);
  return plainMatch?.[1] ?? null;
}

async function financeDownload(path: string): Promise<DownloadedFile> {
  const token = await getAccessToken();
  if (!token) {
    throw new Error("NotAuthenticated");
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Request failed (${response.status})`);
  }

  const blob = await response.blob();
  const fileName =
    parseContentDispositionFileName(response.headers.get("content-disposition")) ??
    "download";

  return { blob, fileName };
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value != null && value !== "") {
      query.set(key, String(value));
    }
  }
  const qs = query.toString();
  return qs ? `?${qs}` : "";
}

/** Partner portal finance endpoints (read-only). */
const partnerBase = "/api/partner/account";

export function fetchFinanceAccount(): Promise<FinancePortalAccount> {
  return financeFetch<FinancePortalAccount>(partnerBase);
}

export function fetchFinancePostings(
  params: FinancePostingsParams = {},
): Promise<PagedResult<FinancePortalPostingRow>> {
  const qs = buildQuery({
    skipCount: params.skipCount,
    maxResultCount: params.maxResultCount,
    from: params.from,
    to: params.to,
  });
  return financeFetch<PagedResult<FinancePortalPostingRow>>(`${partnerBase}/postings${qs}`);
}

export function fetchFinanceDocuments(): Promise<FinanceDocumentListItem[]> {
  return financeFetch<FinanceDocumentListItem[]>(`${partnerBase}/documents`);
}

export function exportFinancePostings(params: FinanceExportParams): Promise<DownloadedFile> {
  const qs = buildQuery({
    format: params.format,
    from: params.from,
    to: params.to,
  });
  return financeDownload(`${partnerBase}/export${qs}`);
}

export function downloadFinanceDocument(documentId: string): Promise<DownloadedFile> {
  return financeDownload(`${partnerBase}/documents/${documentId}/download`);
}
