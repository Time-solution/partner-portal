/** Merchant view tabs — driven by the sidebar entries; legacy query params keep working. */
export type MerchantTab =
  | "dashboard"
  | "browse"
  | "services"
  | "orders"
  | "invoices"
  | "statement"
  | "profile";

/** Legacy params keep working: no param → dashboard, "partners" → services (old default tab). */
export function parseMerchantTab(raw: string | null): MerchantTab {
  if (raw === "partners") return "services";
  if (
    raw === "browse" ||
    raw === "services" ||
    raw === "orders" ||
    raw === "invoices" ||
    raw === "statement" ||
    raw === "profile" ||
    raw === "dashboard"
  ) {
    return raw;
  }
  return "dashboard";
}
