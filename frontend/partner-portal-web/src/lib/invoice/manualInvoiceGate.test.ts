import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  ManualInvoiceGateError,
  evaluateManualInvoiceGate,
  type ManualInvoiceGateContext,
} from "./manualInvoiceGate";
import { computeManualInvoiceLines, computeManualInvoiceTotals } from "./manualInvoice";
import { MockPortalDataSource } from "@/lib/data/mockDataSource";
import { createSeedData } from "@/lib/data/fixtures";
import { savePersistedData } from "@/lib/data/mockStore";
import { translations } from "@/lib/i18n";
import type { ManualInvoice, Partner } from "@/lib/data/types";

function stubLocalStorage() {
  const bag = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => bag.get(k) ?? null,
    setItem: (k: string, v: string) => bag.set(k, v),
    removeItem: (k: string) => bag.delete(k),
    clear: () => bag.clear(),
    key: () => null,
    length: 0,
  });
}

const NOW = new Date("2026-07-16T12:00:00Z");

const activePartner = {
  id: "p-principal",
  legalName: "Principal Co",
  status: "Active",
  participationMode: "Principal",
} as Partner;

const reflectionPartner = {
  id: "p-reflection",
  legalName: "Reflection Co",
  status: "Active",
  participationMode: "ReflectionOnly",
} as Partner;

function invoice(overrides: Partial<ManualInvoice> = {}): ManualInvoice {
  const lines = computeManualInvoiceLines([{ description: "Svc", quantity: 1, unitPriceInclusive: 115 }]);
  const totals = computeManualInvoiceTotals(lines);
  return {
    id: "minv-gate-1",
    invoiceNumber: "MAN-2026-0100",
    source: "Manual",
    status: "Draft",
    recipientType: "Partner",
    recipientReference: activePartner.id,
    recipient: "Principal Co",
    issueDate: "2026-07-15T00:00:00Z",
    currency: "SAR",
    lines,
    subtotalNet: totals.subtotalNet,
    vatTotal: totals.vatTotal,
    grandTotalInclusive: totals.grandTotalInclusive,
    createdAt: "2026-07-15T00:00:00Z",
    idempotencyKey: "gate:1",
    ...overrides,
  };
}

const ctx = (overrides: Partial<ManualInvoiceGateContext> = {}): ManualInvoiceGateContext => ({
  partners: [activePartner, reflectionPartner],
  invoices: [],
  now: NOW,
  ...overrides,
});

describe("P5 gate mirror — per-check pairs + aggregation (parity with the backend gate)", () => {
  beforeEach(() => stubLocalStorage());

  it("happy path: zero violations", () => {
    expect(evaluateManualInvoiceGate(invoice(), ctx())).toEqual([]);
  });

  it(":080 header total mismatch fails; consistent passes", () => {
    const codes = evaluateManualInvoiceGate(invoice({ grandTotalInclusive: 999 }), ctx()).map((v) => v.code);
    expect(codes).toContain("080");
  });

  it(":083 tampered per-line VAT split fails", () => {
    const bad = invoice();
    bad.lines = bad.lines.map((l) => ({ ...l, vatAmount: l.vatAmount + 1, vatNet: l.vatNet - 1 }));
    expect(evaluateManualInvoiceGate(bad, ctx()).map((v) => v.code)).toContain("083");
  });

  it(":084 missing/inactive partner fails; merchant recipients skip", () => {
    expect(
      evaluateManualInvoiceGate(invoice({ recipientReference: "nope" }), ctx()).map((v) => v.code),
    ).toContain("084");
    expect(
      evaluateManualInvoiceGate(
        invoice({ recipientType: "Merchant", recipientReference: "any-merchant" }),
        ctx(),
      ).map((v) => v.code),
    ).not.toContain("084");
  });

  it(":085 duplicate reference fails; unique passes", () => {
    const other = invoice({ id: "minv-other", invoiceNumber: "MAN-2026-0100" });
    expect(
      evaluateManualInvoiceGate(invoice(), ctx({ invoices: [other] })).map((v) => v.code),
    ).toContain("085");
  });

  it(":086 future issue date fails; closed period fails via the seam; open default passes", () => {
    expect(
      evaluateManualInvoiceGate(invoice({ issueDate: "2026-08-01T00:00:00Z" }), ctx()).map((v) => v.code),
    ).toContain("086");
    expect(
      evaluateManualInvoiceGate(invoice(), ctx({ periodOpen: false })).map((v) => v.code),
    ).toContain("086");
    expect(evaluateManualInvoiceGate(invoice(), ctx()).map((v) => v.code)).not.toContain("086");
  });

  it(":088 ReflectionOnly partner blocked; Principal passes; merchant skips", () => {
    expect(
      evaluateManualInvoiceGate(
        invoice({ recipientReference: reflectionPartner.id }),
        ctx(),
      ).map((v) => v.code),
    ).toContain("088");
    expect(evaluateManualInvoiceGate(invoice(), ctx()).map((v) => v.code)).not.toContain("088");
  });

  it("one evaluation aggregates >= 3 codes (no fail-fast) and every code maps to AR+EN i18n", () => {
    const bad = invoice({
      recipientReference: reflectionPartner.id,
      issueDate: "2026-08-01T00:00:00Z",
      grandTotalInclusive: 999,
    });
    const violations = evaluateManualInvoiceGate(bad, ctx({ invoices: [invoice({ id: "dup" })] }));
    const codes = violations.map((v) => v.code);
    expect(new Set(codes).size).toBeGreaterThanOrEqual(3);

    for (const v of violations) {
      expect(translations.ar[v.i18nKey], `AR ${v.i18nKey}`).toBeTruthy();
      expect(translations.en[v.i18nKey], `EN ${v.i18nKey}`).toBeTruthy();
    }
  });

  it("mock issue integration: blocked draft STAYS Draft with the full list; clean draft issues", async () => {
    savePersistedData(createSeedData());
    const ds = new MockPortalDataSource();

    // Make the seeded draft violate: give it a future issue date via a fresh draft instead.
    const future = await ds.createManualInvoice({
      recipientType: "External",
      recipientNameOverride: "Future Co",
      lines: [{ description: "X", quantity: 1, unitPriceInclusive: 115 }],
      issueDate: new Date(Date.now() + 86_400_000).toISOString(),
      idempotencyKey: `gate:future:${Math.random()}`,
    });

    let caught: unknown;
    try {
      await ds.issueManualInvoice(future.id);
    } catch (e) {
      caught = e;
    }
    expect(caught).toBeInstanceOf(ManualInvoiceGateError);
    expect((caught as ManualInvoiceGateError).violations.map((v) => v.code)).toContain("086");
    expect((await ds.getManualInvoice(future.id))?.status).toBe("Draft"); // hard block

    // The seeded Draft (Active SubscriptionFee partner, past date) passes the gate and issues.
    const seededDraft = (await ds.listInvoices({ source: "Manual" })).find(
      (m) => m.id === "minv-seed-partner-1",
    )!;
    expect(seededDraft.status).toBe("Draft");
    const issued = await ds.issueManualInvoice(seededDraft.id);
    expect(issued.status).toBe("Issued");
  });
});
