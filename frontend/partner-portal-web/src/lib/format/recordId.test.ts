import { describe, expect, it } from "vitest";
import { formatOrderRef, formatTenantRef, parseOrderRef, toArabicDigits } from "./recordId";

describe("parseOrderRef", () => {
  it("parses standard order:ord-<n>:v<n> keys", () => {
    expect(parseOrderRef("order:ord-7002:v1")).toEqual({ orderNo: "7002", version: "1" });
  });
  it("parses non ord- prefixed ids", () => {
    expect(parseOrderRef("order:noon-8821:v1")).toEqual({ orderNo: "noon-8821", version: "1" });
  });
  it("handles missing version", () => {
    expect(parseOrderRef("order:ord-9:")).toEqual({ orderNo: "9", version: undefined });
  });
  it("degrades gracefully on unknown shapes", () => {
    expect(parseOrderRef("cs-AM")).toEqual({ orderNo: "cs-AM" });
  });
});

describe("formatOrderRef", () => {
  it("formats EN with version", () => {
    expect(formatOrderRef("order:ord-7002:v1", "en")).toBe("Order #7002 · v1");
  });
  it("formats AR with Arabic-Indic digits and version", () => {
    expect(formatOrderRef("order:ord-7002:v1", "ar")).toBe("طلب رقم ٧٠٠٢ · نسخة ١");
  });
  it("formats without version", () => {
    expect(formatOrderRef("order:ord-7002:", "en")).toBe("Order #7002");
  });
});

describe("toArabicDigits", () => {
  it("converts ASCII to Arabic-Indic digits", () => {
    expect(toArabicDigits("2026")).toBe("٢٠٢٦");
  });
});

describe("formatTenantRef", () => {
  it("prefers a known org name", () => {
    expect(formatTenantRef("11111111-1111-1111-1111-111111111003", "en", "Quick Bites Co.")).toBe(
      "Quick Bites Co.",
    );
  });
  it("masks the raw guid when no org name", () => {
    expect(formatTenantRef("11111111-1111-1111-1111-111111111003", "en")).toBe("Tenant ···111003");
  });
  it("localizes digits in Arabic", () => {
    expect(formatTenantRef("11111111-1111-1111-1111-111111111003", "ar")).toBe("مستأجر ···١١١٠٠٣");
  });
});
