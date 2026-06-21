/**
 * Proforma → HTML layout (PURE). The PDF generator renders THIS markup with html2canvas
 * so the Arabic text (Cairo webfont) and the Riyal currency mark (inline SVG vector, NOT a
 * font glyph) come out sharp — never a tofu box. Separated from the browser capture so the
 * layout/content can be unit-tested in node without a DOM.
 */
import { ZAHY_LOGO_SRC } from "@/components/brand/BrandLogo";
import { moneyMarkup, riyalSvgMarkup } from "@/lib/format/moneyMarkup";
import {
  L,
  PROFORMA_BETA_NOTICE_AR,
  PROFORMA_BETA_NOTICE_EN,
  type ProformaDocument,
  type ProformaParty,
} from "./proformaDocument";

const NAVY = "#023b59";
const MINT = "#d5fde0";
const INK = "#1a1a1a";
const MUTED = "#555555";
const LINE = "#e3e8e9";

function esc(value: string | undefined): string {
  return (value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

/** Back-compat re-export — the single source of the Riyal mark lives in moneyMarkup. */
export const riyalMarkup = riyalSvgMarkup;

/** A money cell: Riyal mark to the LEFT of the 2dp number, dir-locked so RTL never flips it. */
function money(n: number): string {
  return moneyMarkup(n);
}

function partyBlock(title: string, p: ProformaParty, lang: Lang, extra?: string): string {
  const row = (label: string, value?: string) =>
    value ? `<div style="margin:1px 0;"><span style="color:${MUTED};">${esc(label)}:</span> ${esc(value)}</div>` : "";
  return `
    <div data-section="${title === "" ? "party" : "party"}" style="font-size:11px;line-height:1.5;">
      <div style="font-weight:700;color:${NAVY};font-size:12px;margin-bottom:2px;">${esc(title)}</div>
      <div style="font-weight:600;color:${INK};">${esc(p.name)}</div>
      ${extra ? `<div style="color:${NAVY};">${esc(extra)}</div>` : ""}
      ${row(L(lang, "ضريبي", "VAT"), p.vatNumber)}
      ${row(L(lang, "سجل تجاري", "CR"), p.crNumber)}
      ${row(L(lang, "الرقم الوطني", "National no."), p.nationalNumber)}
      ${row(L(lang, "العنوان", "Address"), p.address)}
      ${row(L(lang, "هاتف", "Phone"), p.phone)}
      ${row(L(lang, "بريد", "Email"), p.email)}
    </div>`;
}

type Lang = ProformaDocument["lang"];

/** Build the full A4 proforma HTML document (string). */
export function buildProformaHtml(doc: ProformaDocument, logoSrc = ZAHY_LOGO_SRC): string {
  const lang = doc.lang;
  const dir = lang === "ar" ? "rtl" : "ltr";
  const align = lang === "ar" ? "right" : "left";
  const t = doc.totals;

  const betaNotice = lang === "ar" ? PROFORMA_BETA_NOTICE_AR : PROFORMA_BETA_NOTICE_EN;

  const lineRows = doc.lines
    .map(
      (l) => `
      <tr>
        <td style="padding:7px 8px;border-bottom:1px solid ${LINE};">${esc(l.serviceType)}</td>
        <td style="padding:7px 8px;border-bottom:1px solid ${LINE};">${esc(l.billingType)}</td>
        <td style="padding:7px 8px;border-bottom:1px solid ${LINE};color:${MUTED};">${esc(l.basis)}</td>
        <td style="padding:7px 8px;border-bottom:1px solid ${LINE};text-align:end;">${money(l.exVat)}</td>
        <td style="padding:7px 8px;border-bottom:1px solid ${LINE};text-align:end;">${money(l.vat)}</td>
        <td style="padding:7px 8px;border-bottom:1px solid ${LINE};text-align:end;font-weight:600;">${money(l.inclusive)}</td>
      </tr>`,
    )
    .join("");

  const via =
    doc.kind === "invoice" && doc.viaPartnerName
      ? L(lang, `عبر ${doc.viaPartnerName}`, `via ${doc.viaPartnerName}`)
      : undefined;

  const totalRow = (label: string, value: string, strong = false) => `
      <tr>
        <td style="padding:5px 8px;text-align:${align};color:${MUTED};">${esc(label)}</td>
        <td style="padding:5px 8px;text-align:end;font-weight:${strong ? 800 : 600};${strong ? `color:${NAVY};font-size:15px;` : ""}">${value}</td>
      </tr>`;

  return `<!doctype html><html lang="${lang}" dir="${dir}"><head><meta charset="utf-8">
  <style>
    *{box-sizing:border-box;}
    @import url('https://fonts.googleapis.com/css2?family=Cairo:wght@400;600;700;800&display=swap');
  </style></head>
  <body style="margin:0;">
  <div data-proforma-root dir="${dir}" lang="${lang}" style="position:relative;width:794px;min-height:1123px;padding:48px;background:#ffffff;color:${INK};font-family:Cairo,'Segoe UI',Tahoma,sans-serif;font-size:12px;text-align:${align};">

    <!-- BETA watermark -->
    <div data-section="watermark" style="position:absolute;top:46%;left:0;right:0;text-align:center;transform:rotate(-22deg);font-size:96px;font-weight:800;color:rgba(2,59,89,0.07);pointer-events:none;letter-spacing:6px;">BETA · تجريبي</div>

    <!-- HEADER -->
    <div data-section="header" style="display:flex;justify-content:space-between;align-items:flex-start;gap:16px;border-bottom:3px solid ${NAVY};padding-bottom:14px;">
      <div style="max-width:60%;">
        <img src="${esc(logoSrc)}" alt="Zahy" crossorigin="anonymous" style="height:46px;width:auto;object-fit:contain;display:block;margin-bottom:8px;" />
        ${partyBlock(L(lang, "الجهة المُصدِرة", "Issuer"), doc.issuer, lang)}
      </div>
      <div style="text-align:end;min-width:36%;">
        <div data-section="doctype" style="display:inline-block;background:${MINT};color:${NAVY};font-weight:800;padding:6px 12px;border-radius:8px;font-size:13px;">${esc(doc.docTypeLabel)}</div>
        <div style="margin-top:10px;font-size:11px;line-height:1.7;">
          <div><span style="color:${MUTED};">${esc(L(lang, "رقم المستند", "Document no."))}:</span> <strong>${esc(doc.docNumber)}</strong></div>
          <div><span style="color:${MUTED};">${esc(L(lang, "الفترة", "Period"))}:</span> ${esc(doc.periodLabel)}</div>
          <div><span style="color:${MUTED};">${esc(L(lang, "تاريخ الإصدار", "Issue date"))}:</span> ${esc(doc.issueDate)}</div>
        </div>
      </div>
    </div>

    <!-- BILL TO -->
    <div data-section="billto" style="margin-top:18px;padding:12px 14px;border:1px solid ${LINE};border-radius:8px;background:#f7fafa;">
      <div style="color:${MUTED};font-size:10px;font-weight:700;letter-spacing:1px;margin-bottom:4px;">${esc(L(lang, "إلى / فاتورة إلى", "BILL TO"))}</div>
      ${partyBlock(doc.kind === "invoice" ? L(lang, "التاجر / الجهة", "Merchant / Entity") : L(lang, "الشريك", "Partner"), doc.billTo, lang, via)}
      ${doc.serviceContext ? `<div style="margin-top:6px;font-size:11px;color:${NAVY};">${esc(L(lang, "نوع الخدمة", "Service"))}: ${esc(doc.serviceContext)}</div>` : ""}
    </div>

    <!-- BODY: line items -->
    <table data-section="lines" dir="${dir}" style="width:100%;border-collapse:collapse;margin-top:18px;font-size:11px;">
      <thead>
        <tr style="background:${NAVY};color:#ffffff;">
          <th style="padding:8px;text-align:${align};font-weight:700;">${esc(L(lang, "الخدمة", "Service"))}</th>
          <th style="padding:8px;text-align:${align};font-weight:700;">${esc(L(lang, "نوع الفوترة", "Billing"))}</th>
          <th style="padding:8px;text-align:${align};font-weight:700;">${esc(L(lang, "الأساس", "Basis"))}</th>
          <th style="padding:8px;text-align:end;font-weight:700;">${esc(L(lang, "قبل الضريبة", "Ex-VAT"))}</th>
          <th style="padding:8px;text-align:end;font-weight:700;">${esc(L(lang, "ضريبة", "VAT"))}</th>
          <th style="padding:8px;text-align:end;font-weight:700;">${esc(L(lang, "شامل الضريبة", "Incl. VAT"))}</th>
        </tr>
      </thead>
      <tbody>${lineRows}</tbody>
    </table>

    <!-- TOTALS -->
    <div style="display:flex;justify-content:${lang === "ar" ? "flex-start" : "flex-end"};margin-top:16px;">
      <table data-section="totals" style="min-width:280px;border-collapse:collapse;font-size:12px;">
        ${totalRow(L(lang, "الإجمالي قبل الضريبة", "Subtotal (ex-VAT)"), money(t.subtotalExVat))}
        ${totalRow(L(lang, "إجمالي ضريبة القيمة المضافة", "Total VAT"), money(t.totalVat))}
        ${totalRow(L(lang, "الإجمالي شامل الضريبة", "GRAND TOTAL (incl.)"), money(t.grandTotalInclusive), true)}
        ${totalRow(L(lang, "المسدد حتى تاريخه", "Paid to date"), money(t.paidToDate))}
        ${totalRow(L(lang, "المتبقي", "Remaining"), money(t.remaining))}
      </table>
    </div>

    <!-- FOOTER -->
    <div data-section="footer" style="position:absolute;left:48px;right:48px;bottom:36px;border-top:1px solid ${LINE};padding-top:10px;font-size:9px;color:${MUTED};display:flex;justify-content:space-between;align-items:center;gap:12px;">
      <img src="${esc(logoSrc)}" alt="Zahy" crossorigin="anonymous" style="height:18px;width:auto;object-fit:contain;" />
      <span style="flex:1;text-align:center;">${esc(betaNotice)} · ${esc(doc.issuer.email)} · ${esc(doc.issuer.phone)}</span>
      <span data-section="pageno">${esc(L(lang, "صفحة 1 من 1", "Page 1 of 1"))}</span>
    </div>

  </div></body></html>`;
}
