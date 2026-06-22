/**
 * Payment-receipt → HTML layout (PURE). Rendered by the receipt PDF generator with html2canvas —
 * the SAME pipeline the proforma uses — so Arabic shapes correctly (Cairo webfont) and the Riyal
 * currency mark is a sharp inline SVG vector (never the old jsPDF-core mojibake like "þ©þ•þª" / a
 * "SAR" string). Separated from the browser capture so the layout/content is unit-testable in node.
 *
 * Single source: reuses the proforma's `moneyMarkup` / `riyalSvgMarkup`, the `L` bilingual helper and
 * the shared brand logo. DISPLAY ONLY — no money math / ledger / posting is touched here.
 */
import { ZAHY_LOGO_SRC } from "@/components/brand/BrandLogo";
import { moneyMarkup, riyalSvgMarkup } from "@/lib/format/moneyMarkup";
import { L } from "@/lib/invoice/proformaDocument";
import type { BankAccount } from "@/lib/banks/bankAccount";
import type { Lang } from "@/lib/i18n";
import type { PaymentMethod } from "@/lib/data/types";

const NAVY = "#023b59";
const MINT = "#d5fde0";
const INK = "#1a1a1a";
const MUTED = "#555555";
const LINE = "#e3e8e9";

/** BETA footer marks — proof of payment, NOT a ZATCA tax invoice. */
export const RECEIPT_BETA_NOTICE_AR = "إثبات دفع (تجريبي) — وليس فاتورة ضريبية متوافقة مع هيئة الزكاة والضريبة والجمارك.";
export const RECEIPT_BETA_NOTICE_EN = "BETA — proof of payment, not a ZATCA tax invoice.";

function esc(value: string | undefined): string {
  return (value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function methodLabel(lang: Lang, method: PaymentMethod): string {
  switch (method) {
    case "COD":
      return L(lang, "دفع عند الاستلام", "Cash on delivery");
    case "Online":
      return L(lang, "دفع إلكتروني", "Online");
    case "Transfer":
    default:
      return L(lang, "تحويل بنكي", "Bank transfer");
  }
}

/** The pure receipt model the HTML builder renders (already resolved — no store access here). */
export interface ReceiptDoc {
  lang: Lang;
  receiptNo: string;
  date: string;
  merchantName: string;
  invoiceRef: string;
  method: PaymentMethod;
  amount: number;
  currency: string;
  /** Pre-resolved bank/destination label (see {@link receiptBankLabel}). */
  bankLabel: string;
}

/**
 * Resolve the display label for where the payment landed, from the Track B bank registry. Returns
 * "{bank name} · ••••{last4}" when the payment carries a known bank; otherwise the 1100 parent
 * fallback label. PURE — the caller passes the banks list (fetched from the registry store).
 */
export function receiptBankLabel(
  bankAccountId: string | undefined,
  banks: readonly BankAccount[],
  lang: Lang,
): string {
  const bank = bankAccountId ? banks.find((b) => b.id === bankAccountId) : undefined;
  if (bank) {
    const last4 = bank.accountNumber.trim().slice(-4);
    return `${bank.name} · ••••${last4}`;
  }
  return L(lang, "الحساب الأب 1100 (نقد / بنك)", "1100 parent (Cash / Bank)");
}

/** Money group: Riyal mark to the LEFT of the number, dir-locked (shared single source). */
function money(n: number): string {
  return moneyMarkup(n);
}

/** Build the full A4 receipt HTML document (string). Bilingual; AR = RTL, EN = LTR. */
export function buildReceiptHtml(doc: ReceiptDoc, logoSrc = ZAHY_LOGO_SRC): string {
  const { lang } = doc;
  const dir = lang === "ar" ? "rtl" : "ltr";
  const align = lang === "ar" ? "right" : "left";
  const betaNotice = lang === "ar" ? RECEIPT_BETA_NOTICE_AR : RECEIPT_BETA_NOTICE_EN;

  const detailRow = (label: string, value: string) => `
      <tr>
        <td style="padding:9px 8px;border-bottom:1px solid ${LINE};color:${MUTED};">${esc(label)}</td>
        <td style="padding:9px 8px;border-bottom:1px solid ${LINE};text-align:end;font-weight:600;color:${INK};">${value}</td>
      </tr>`;

  return `<!doctype html><html lang="${lang}" dir="${dir}"><head><meta charset="utf-8">
  <style>
    *{box-sizing:border-box;}
    @import url('https://fonts.googleapis.com/css2?family=Cairo:wght@400;600;700;800&display=swap');
  </style></head>
  <body style="margin:0;">
  <div data-receipt-root dir="${dir}" lang="${lang}" style="position:relative;width:794px;min-height:1123px;padding:48px;background:#ffffff;color:${INK};font-family:Cairo,'Segoe UI',Tahoma,sans-serif;font-size:12px;text-align:${align};">

    <!-- BETA watermark -->
    <div data-section="watermark" style="position:absolute;top:44%;left:0;right:0;text-align:center;transform:rotate(-22deg);font-size:96px;font-weight:800;color:rgba(2,59,89,0.07);pointer-events:none;letter-spacing:6px;">BETA · تجريبي</div>

    <!-- HEADER -->
    <div data-section="header" style="display:flex;justify-content:space-between;align-items:flex-start;gap:16px;border-bottom:3px solid ${NAVY};padding-bottom:14px;">
      <div style="max-width:60%;">
        <img src="${esc(logoSrc)}" alt="Zahy" crossorigin="anonymous" style="height:46px;width:auto;object-fit:contain;display:block;margin-bottom:8px;" />
        <div style="font-weight:700;color:${NAVY};font-size:13px;">${esc(L(lang, "منصة شركاء زاهي", "Zahy Partner Platform"))}</div>
      </div>
      <div style="text-align:end;min-width:36%;">
        <div data-section="doctype" style="display:inline-block;background:${MINT};color:${NAVY};font-weight:800;padding:6px 12px;border-radius:8px;font-size:13px;">${esc(L(lang, "إيصال سداد", "Payment receipt"))}</div>
        <div style="margin-top:10px;font-size:11px;line-height:1.7;">
          <div><span style="color:${MUTED};">${esc(L(lang, "رقم الإيصال", "Receipt No"))}:</span> <strong>${esc(doc.receiptNo)}</strong></div>
          <div><span style="color:${MUTED};">${esc(L(lang, "التاريخ", "Date"))}:</span> ${esc(doc.date)}</div>
        </div>
      </div>
    </div>

    <!-- DETAILS -->
    <table data-section="details" dir="${dir}" style="width:100%;border-collapse:collapse;margin-top:22px;font-size:12px;">
      <tbody>
        ${detailRow(L(lang, "المدفوع من", "Paid by"), esc(doc.merchantName))}
        ${detailRow(L(lang, "مرجع الفاتورة", "Invoice ref"), esc(doc.invoiceRef))}
        ${detailRow(L(lang, "طريقة الدفع", "Payment method"), esc(methodLabel(lang, doc.method)))}
        ${detailRow(L(lang, "مستلَم في", "Received to"), esc(doc.bankLabel))}
        ${detailRow(L(lang, "المبلغ المستلم", "Amount received"), money(doc.amount))}
      </tbody>
    </table>

    <!-- TOTAL EMPHASIS -->
    <div data-section="total" style="margin-top:18px;display:flex;justify-content:space-between;align-items:center;background:#f7fafa;border:1px solid ${LINE};border-radius:8px;padding:16px 18px;">
      <span style="color:${MUTED};font-weight:600;">${esc(L(lang, "إجمالي المستلم", "Total received"))}</span>
      <span style="font-weight:800;color:${NAVY};font-size:18px;">${money(doc.amount)}</span>
    </div>

    <!-- FOOTER -->
    <div data-section="footer" style="position:absolute;left:48px;right:48px;bottom:36px;border-top:1px solid ${LINE};padding-top:10px;font-size:9px;color:${MUTED};display:flex;justify-content:space-between;align-items:center;gap:12px;">
      <img src="${esc(logoSrc)}" alt="Zahy" crossorigin="anonymous" style="height:18px;width:auto;object-fit:contain;" />
      <span style="flex:1;text-align:center;">${esc(betaNotice)}</span>
      <span data-section="pageno">${esc(L(lang, "صفحة 1 من 1", "Page 1 of 1"))}</span>
    </div>

  </div></body></html>`;
}

/** Re-export so tests can assert the shared Riyal vector is embedded. */
export const receiptRiyalMarkup = riyalSvgMarkup;
