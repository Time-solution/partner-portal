/**
 * Manual invoice PDF download.
 *
 * LIVE: the server-rendered PDF is authoritative — `GET /api/finance/invoices/{id}/pdf`
 * renders from the keyed line items (NOT the ledger) and carries the BETA mark + ZATCA
 * shaping. We stream it as a Blob and trigger a browser download.
 *
 * MOCK (default, flagged OFF): there is no backend, so we render the SAME document
 * client-side via the existing proforma PDF pipeline (sharp Arabic + Riyal SVG), which
 * also carries the BETA proforma mark. The money is taken verbatim from the invoice —
 * never recomputed here.
 */
import { isMockDataSource } from "@/lib/data/config";
import type { ManualInvoice } from "@/lib/data/types";
import type { Lang } from "@/lib/i18n";
import { downloadProformaPdf } from "@/lib/invoice/proformaPdf";
import { buildManualInvoiceProforma } from "@/lib/invoice/manualInvoiceProforma";

function triggerBrowserDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

async function fetchServerPdf(id: string): Promise<{ blob: Blob; fileName: string }> {
  // Lazily load the OIDC user manager — it touches window/localStorage at construction,
  // so it must never be pulled into the mock/test (node) import graph.
  const { userManager } = await import("@/lib/auth/userManager");
  const { apiBaseUrl } = await import("@/lib/auth/oidcConfig");
  const user = await userManager.getUser();
  if (!user || user.expired) {
    throw new Error("NotAuthenticated");
  }
  const response = await fetch(`${apiBaseUrl}/api/finance/invoices/${id}/pdf`, {
    headers: { Authorization: `Bearer ${user.access_token}` },
  });
  if (!response.ok) {
    throw new Error((await response.text()) || `Request failed (${response.status})`);
  }
  const blob = await response.blob();
  return { blob, fileName: `${id}.pdf` };
}

/** Download (or preview) the PDF for a manual invoice. */
export async function downloadManualInvoicePdf(invoice: ManualInvoice, lang: Lang): Promise<void> {
  if (isMockDataSource()) {
    await downloadProformaPdf(buildManualInvoiceProforma(invoice, lang));
    return;
  }
  const file = await fetchServerPdf(invoice.id);
  triggerBrowserDownload(file.blob, file.fileName);
}
