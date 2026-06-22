/**
 * Payment-receipt PDF generator (browser only). Rebuilt to use the SAME html2canvas → jsPDF
 * pipeline as the proforma: render {@link buildReceiptHtml} in an offscreen iframe, WAIT for the
 * Cairo webfont + logo image, capture with html2canvas, then place it on an A4 jsPDF page.
 *
 * This fixes the old jsPDF-core output, which could neither shape connected Arabic (mojibake like
 * "þ©þ•þª") nor render the Riyal SVG (it fell back to a "SAR" string). Capturing rendered HTML makes
 * Arabic sharp and the Riyal a crisp vector. DISPLAY ONLY — no money math / ledger change.
 */
import type { Receipt } from "@/lib/data/types";
import type { Lang } from "@/lib/i18n";
import { listBankAccounts } from "@/lib/banks/bankRegistryStore";
import { buildReceiptHtml, receiptBankLabel, type ReceiptDoc } from "./receiptHtml";

const A4_WIDTH_PT = 595.28;
const A4_HEIGHT_PT = 841.89;
const RENDER_WIDTH_PX = 794;
const RENDER_HEIGHT_PX = 1123;

function fmtDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toISOString().slice(0, 10);
}

/** Map a receipt + the chosen language into the pure HTML model (resolves the bank label here). */
export function toReceiptDoc(receipt: Receipt, lang: Lang): ReceiptDoc {
  const banks = listBankAccounts();
  return {
    lang,
    receiptNo: receipt.receiptNo,
    date: fmtDate(receipt.date),
    merchantName: receipt.merchantName,
    invoiceRef: receipt.invoiceRef,
    method: receipt.method,
    amount: receipt.amount.amount,
    currency: receipt.amount.currency,
    bankLabel: receiptBankLabel(receipt.bankAccountId, banks, lang),
  };
}

async function waitForAssets(idoc: Document, win: Window | null): Promise<void> {
  const images = Array.from(idoc.images);
  await Promise.all(
    images.map((img) =>
      img.complete
        ? Promise.resolve()
        : new Promise<void>((resolve) => {
            img.addEventListener("load", () => resolve(), { once: true });
            img.addEventListener("error", () => resolve(), { once: true });
          }),
    ),
  );
  try {
    const fonts = (idoc as Document & { fonts?: FontFaceSet }).fonts;
    if (fonts?.ready) await fonts.ready;
    if (typeof document !== "undefined" && document.fonts?.ready) await document.fonts.ready;
  } catch {
    /* fonts API unavailable — fall through */
  }
  await new Promise((r) => (win ?? window).setTimeout(r, 250));
}

async function renderToCanvas(doc: ReceiptDoc): Promise<HTMLCanvasElement> {
  const html2canvas = (await import("html2canvas")).default;

  const iframe = document.createElement("iframe");
  iframe.setAttribute("aria-hidden", "true");
  Object.assign(iframe.style, {
    position: "fixed",
    left: "-10000px",
    top: "0",
    width: `${RENDER_WIDTH_PX}px`,
    height: `${RENDER_HEIGHT_PX}px`,
    border: "0",
    background: "#ffffff",
  });
  document.body.appendChild(iframe);

  try {
    const idoc = iframe.contentDocument!;
    idoc.open();
    idoc.write(buildReceiptHtml(doc));
    idoc.close();

    await waitForAssets(idoc, iframe.contentWindow);

    const root = (idoc.querySelector("[data-receipt-root]") as HTMLElement) ?? idoc.body;
    return await html2canvas(root, {
      scale: 2,
      useCORS: true,
      backgroundColor: "#ffffff",
      windowWidth: RENDER_WIDTH_PX,
      windowHeight: RENDER_HEIGHT_PX,
    });
  } finally {
    iframe.remove();
  }
}

/** Build the receipt PDF (in the chosen language) and trigger a browser download. */
export async function downloadReceiptPdf(receipt: Receipt, lang: Lang = "ar"): Promise<void> {
  const { jsPDF } = await import("jspdf");
  const canvas = await renderToCanvas(toReceiptDoc(receipt, lang));

  const pdf = new jsPDF({ unit: "pt", format: "a4" });
  const imgData = canvas.toDataURL("image/png");
  const imgHeight = Math.min(A4_HEIGHT_PT, (canvas.height * A4_WIDTH_PT) / canvas.width);
  pdf.addImage(imgData, "PNG", 0, 0, A4_WIDTH_PT, imgHeight, undefined, "FAST");
  pdf.save(`${receipt.receiptNo}.pdf`);
}
