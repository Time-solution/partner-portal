/**
 * Proforma PDF generator (browser only). Renders {@link buildProformaHtml} in an offscreen
 * iframe, WAITS for the Cairo webfont + the logo image to load, then captures it with
 * html2canvas and places it on an A4 jsPDF page. Capturing rendered HTML (rather than drawing
 * with jsPDF's WinAnsi core fonts) is what makes the Arabic text and the Riyal SVG come out
 * sharp instead of tofu boxes. jspdf + html2canvas are lazy-loaded.
 */
import type { ProformaDocument } from "./proformaDocument";
import { buildProformaHtml } from "./proformaHtml";

const A4_WIDTH_PT = 595.28;
const A4_HEIGHT_PT = 841.89;
const RENDER_WIDTH_PX = 794;
const RENDER_HEIGHT_PX = 1123;

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
    // The parent document also hosts Cairo; make sure it is settled too.
    if (typeof document !== "undefined" && document.fonts?.ready) await document.fonts.ready;
  } catch {
    /* fonts API unavailable — fall through */
  }
  // Small settle so the @import webfont paints before capture.
  await new Promise((r) => (win ?? window).setTimeout(r, 250));
}

async function renderToCanvas(doc: ProformaDocument): Promise<HTMLCanvasElement> {
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
    idoc.write(buildProformaHtml(doc));
    idoc.close();

    await waitForAssets(idoc, iframe.contentWindow);

    const root = (idoc.querySelector("[data-proforma-root]") as HTMLElement) ?? idoc.body;
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

/** Build the proforma PDF and trigger a browser download. */
export async function downloadProformaPdf(doc: ProformaDocument): Promise<void> {
  const { jsPDF } = await import("jspdf");
  const canvas = await renderToCanvas(doc);

  const pdf = new jsPDF({ unit: "pt", format: "a4" });
  const imgData = canvas.toDataURL("image/png");
  // Fit the capture to the A4 page width; height scales with the capture aspect ratio.
  const imgHeight = Math.min(A4_HEIGHT_PT, (canvas.height * A4_WIDTH_PT) / canvas.width);
  pdf.addImage(imgData, "PNG", 0, 0, A4_WIDTH_PT, imgHeight, undefined, "FAST");
  pdf.save(`${doc.docNumber}.pdf`);
}
