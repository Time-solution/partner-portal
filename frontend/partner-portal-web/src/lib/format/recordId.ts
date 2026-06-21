import type { Lang } from "@/lib/i18n";

const AR_DIGITS = ["٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩"];

/** Convert ASCII digits to Arabic-Indic digits (display only). */
export function toArabicDigits(value: string): string {
  return value.replace(/[0-9]/g, (d) => AR_DIGITS[Number(d)]);
}

function localizeDigits(value: string, lang: Lang): string {
  return lang === "ar" ? toArabicDigits(value) : value;
}

export interface ParsedOrderRef {
  /** User-facing order number (e.g. "7002"). */
  orderNo: string;
  /** Status-history version number without the "v" prefix (e.g. "1"), if present. */
  version?: string;
}

/**
 * Parse a raw order key like "order:ord-7002:v1" → { orderNo: "7002", version: "1" }.
 * Unknown shapes degrade gracefully (whole string used as orderNo).
 */
export function parseOrderRef(raw: string): ParsedOrderRef {
  if (!raw) return { orderNo: "" };
  const parts = raw.split(":");
  if (parts[0] === "order" && parts.length >= 2) {
    const orderNo = parts[1].replace(/^ord-/i, "");
    const version = parts[2]?.replace(/^v/i, "") || undefined;
    return { orderNo, version };
  }
  return { orderNo: raw };
}

/**
 * Clean, user-facing order display.
 *   EN: "Order #7002 · v1"
 *   AR: "طلب رقم ٧٠٠٢ · نسخة ١"
 * Keep the raw id for tooltips/details via the original value.
 */
export function formatOrderRef(raw: string, lang: Lang): string {
  const { orderNo, version } = parseOrderRef(raw);
  if (lang === "ar") {
    const base = `طلب رقم ${toArabicDigits(orderNo)}`;
    return version ? `${base} · نسخة ${toArabicDigits(version)}` : base;
  }
  const base = `Order #${orderNo}`;
  return version ? `${base} · v${version}` : base;
}

/**
 * Clean tenant/org id display — prefers a known org name, else a short masked tenant ref
 * (last id segment) instead of the raw GUID.
 *   EN: "Tenant ···111001"  AR: "مستأجر ···١١١٠٠١"
 */
export function formatTenantRef(tenantId: string, lang: Lang, orgName?: string): string {
  if (orgName) return orgName;
  if (!tenantId) return "—";
  const tail = tenantId.split("-").pop() ?? tenantId;
  const shortTail = tail.length > 6 ? tail.slice(-6) : tail;
  const prefix = lang === "ar" ? "مستأجر" : "Tenant";
  return `${prefix} ···${localizeDigits(shortTail, lang)}`;
}
