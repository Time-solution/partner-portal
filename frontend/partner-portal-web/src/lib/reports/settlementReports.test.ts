import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { buildResaleJournal, deriveSettlementSummary } from "@/lib/data/types";
import type { PortalData, SettlementCase, SubscriptionBillingPeriod } from "@/lib/data/types";
import { mockPortalData } from "@/lib/data/fixtures";
import { scopePortalData } from "@/lib/org/orgScope";
import type { OrgContext } from "@/lib/org/orgModel";
import {
  VAT_RATE,
  merchantStatement,
  partnerStatement,
  platformTotals,
  splitInclusiveVat,
  summarizeJournalLines,
  toReportEntries,
  trialBalance,
  vatControl,
} from "./settlementReports";

const CHEFZ_PARTNER = "22222222-2222-2222-2222-222222222006";
const TENANT_SHAWARMA = "11111111-1111-1111-1111-111111111006";

const PARTNER_A = "partner-a";
const PARTNER_B = "partner-b";
const M1 = "merchant-1";
const M2 = "merchant-2";
const M3 = "merchant-3";

function resaleCase(
  id: string,
  partnerId: string,
  tenantId: string,
  sellGross: number,
  buyGross: number,
  createdAt: string,
): SettlementCase {
  return {
    id,
    partnerId,
    partnerName: partnerId,
    tenantId,
    externalTransactionId: `order:${id}:v1`,
    book: "Marketplace",
    state: "Allocated",
    journal: buildResaleJournal({ sellGross, buyGross, postedAt: createdAt }),
    createdAt,
  };
}

function feePeriod(
  id: string,
  partnerId: string,
  tenantId: string,
  feeInclusive: number,
  netFee: number,
  outputVat: number,
  periodKey: string,
): SubscriptionBillingPeriod {
  return {
    id,
    partnerId,
    tenantId,
    merchantName: tenantId,
    periodKey,
    feeInclusive: { amount: feeInclusive, currency: "SAR", vatInclusive: true },
    outputVat,
    netFee,
    billingChargeId: `chg-${id}`,
    invoiceNumber: `INV-${id}`,
    journalBalanced: true,
    status: "Invoiced",
  };
}

/** Scenario S = {Principal 70->100, Principal 10->13, SubscriptionFee 100 incl}. */
function scenarioS(): PortalData {
  const base = {
    partners: [
      { id: PARTNER_A, legalName: "A", tradeName: "A", type: "Carrier", status: "Active", primaryContactEmail: "a@a", participationMode: "Principal", accentClass: "" },
      { id: PARTNER_B, legalName: "B", tradeName: "B", type: "Service", status: "Active", primaryContactEmail: "b@b", participationMode: "SubscriptionFee", accentClass: "" },
    ],
    activations: [
      { id: "a1", partnerId: PARTNER_A, tenantId: M1, merchantName: "M1", catalogItemId: "c1", catalogItemName: "x", resalePrice: { amount: 100, currency: "SAR", vatInclusive: true }, status: "Active" },
      { id: "a2", partnerId: PARTNER_A, tenantId: M2, merchantName: "M2", catalogItemId: "c1", catalogItemName: "x", resalePrice: { amount: 13, currency: "SAR", vatInclusive: true }, status: "Active" },
      { id: "a3", partnerId: PARTNER_B, tenantId: M3, merchantName: "M3", catalogItemId: "c2", catalogItemName: "y", resalePrice: { amount: 100, currency: "SAR", vatInclusive: true }, status: "Active" },
    ],
    activationWorkflows: [],
    settlementCases: [
      resaleCase("s100", PARTNER_A, M1, 100, 70, "2026-06-03T10:00:00Z"),
      resaleCase("s13", PARTNER_A, M2, 13, 10, "2026-06-04T10:00:00Z"),
    ],
    reversals: [],
    reflectedOrders: [],
    billingPeriods: [feePeriod("2026-06", PARTNER_B, M3, 100, 86.96, 13.04, "2026-06")],
    receipts: [],
    webhookEndpoints: [],
    webhookDeliveries: [],
    credentials: [],
    kpis: {} as PortalData["kpis"],
    teams: [],
    auditLog: [],
    users: [],
    orgs: [],
    orgUsers: [],
  };
  return base as unknown as PortalData;
}

describe("settlementReports — scenario S (single source)", () => {
  const entries = toReportEntries(scenarioS());

  it("trial balance nets to zero", () => {
    const tb = trialBalance(entries);
    expect(tb.balanced).toBe(true);
    expect(tb.totalDebits).toBe(tb.totalCredits);
  });

  it("platform totals match the anchor figures", () => {
    const p = platformTotals(entries);
    expect(p.resaleMargin).toBe(28.69); // 98.26 − 69.57
    expect(p.feeRevenue).toBe(86.96);
    expect(p.outputVat).toBe(27.78); // 13.04 + 1.70 + 13.04
    expect(p.inputVat).toBe(10.43); // 9.13 + 1.30
    expect(p.netVatToZatca).toBe(17.35);
  });

  it("partner payable total = 80 (70 + 10), fee partner has none", () => {
    expect(partnerStatement(entries, PARTNER_A).payable).toBe(80);
    expect(partnerStatement(entries, PARTNER_B).payable).toBe(0);
  });

  it("merchant receivable across all = 213 (100 + 13 + 100 fee)", () => {
    const total =
      merchantStatement(entries, M1).receivable +
      merchantStatement(entries, M2).receivable +
      merchantStatement(entries, M3).receivable;
    expect(r2(total)).toBe(213);
    expect(merchantStatement(entries, M3).fees).toBe(86.96);
  });

  it("statements filter to their own rows only", () => {
    expect(partnerStatement(entries, PARTNER_A).entries.every((e) => e.partnerId === PARTNER_A)).toBe(true);
    expect(merchantStatement(entries, M1).entries.every((e) => e.tenantId === M1)).toBe(true);
    expect(merchantStatement(entries, M1).orderCount).toBe(1);
  });
});

describe("net-VAT card reads VatControlReport (no 15% shortcut)", () => {
  const entries = toReportEntries(scenarioS());

  it("net VAT card value = VatControlReport (2200 − 1300) = 17.35 for scenario S", () => {
    const vat = vatControl(entries);
    expect(vat.outputVat).toBe(27.78);
    expect(vat.inputVat).toBe(10.43);
    expect(vat.netVatToZatca).toBe(17.35);
    // The card reads the read model — identical to platformTotals.
    expect(vat.netVatToZatca).toBe(platformTotals(entries).netVatToZatca);
  });

  it("net VAT is NOT a flat 15% of any total (margin/fee/collected)", () => {
    const vat = vatControl(entries).netVatToZatca;
    const p = platformTotals(entries);
    const flatOf = (n: number) => Math.round(n * 0.15 * 100) / 100;
    expect(vat).not.toBe(flatOf(p.resaleMargin));
    expect(vat).not.toBe(flatOf(p.feeRevenue));
    expect(vat).not.toBe(flatOf(p.collectedTotal));
  });

  it("finance summary card source uses no hardcoded 0.15 / 15% VAT shortcut", () => {
    for (const file of [
      "src/features/admin/pages/FinanceWorkspacePage.tsx",
      "src/features/admin/pages/ReportsPage.tsx",
      "src/features/admin/components/FinanceDrillDown.tsx",
    ]) {
      const src = readFileSync(file, "utf8");
      expect(src, `${file} must not multiply by 0.15`).not.toMatch(/\*\s*0\.15/);
      expect(src, `${file} must not divide by 1.15 for a card`).not.toMatch(/\/\s*1\.15/);
    }
  });
});

describe("consolidation — one source for net-VAT / margin / fee", () => {
  it("splitInclusiveVat is the single inclusive→ex-VAT+VAT formula (anchors)", () => {
    expect(VAT_RATE).toBe(0.15);
    expect(splitInclusiveVat(1.0)).toEqual({ exVat: 0.87, vat: 0.13 });
    expect(splitInclusiveVat(5.0)).toEqual({ exVat: 4.35, vat: 0.65 });
    expect(splitInclusiveVat(40.0)).toEqual({ exVat: 34.78, vat: 5.22 });
    expect(splitInclusiveVat(100.0)).toEqual({ exVat: 86.96, vat: 13.04 });
  });

  it("deriveSettlementSummary delegates to summarizeJournalLines (no parallel math)", () => {
    for (const c of scenarioS().settlementCases) {
      const owned = summarizeJournalLines(c.journal.lines);
      const summary = deriveSettlementSummary(c.journal);
      expect(summary.outputVat).toBe(owned.outputVat);
      expect(summary.inputVat).toBe(owned.inputVat);
      expect(summary.netVatToZatca).toBe(owned.netVatToZatca);
      expect(summary.margin).toBe(owned.resaleMargin);
    }
  });

  it("consolidated scenario-S figures equal the prior anchors (17.35 / 28.69 / 86.96)", () => {
    const data = scenarioS();
    const entries = toReportEntries(data);

    // Margin from the per-case path (deriveSettlementSummary) ties to the platform total.
    const caseMargin = r2(
      data.settlementCases.reduce((s, c) => s + deriveSettlementSummary(c.journal).margin, 0),
    );
    expect(caseMargin).toBe(28.69);
    expect(caseMargin).toBe(platformTotals(entries).resaleMargin);

    expect(vatControl(entries).netVatToZatca).toBe(17.35);
    expect(platformTotals(entries).feeRevenue).toBe(86.96);
  });

  it("no module outside the helper carries a 0.15 / 1.15 VAT shortcut", () => {
    for (const file of [
      "src/lib/data/types.ts",
      "src/lib/reports/activationFees.ts",
      "src/lib/analytics/chartAnalytics.ts",
      "src/lib/export/settlementExport.ts",
    ]) {
      const src = readFileSync(file, "utf8");
      expect(src, `${file} must not reference 0.15`).not.toMatch(/0\.15/);
      expect(src, `${file} must not divide by 1.15`).not.toMatch(/1\.15/);
    }
    // The single home keeps the literal exactly once.
    expect(readFileSync("src/lib/reports/settlementReports.ts", "utf8")).toContain("VAT_RATE = 0.15");
  });
});

describe("settlementReports — ReflectionOnly contributes count only", () => {
  it("reflection orders add a count but no money", () => {
    const data = scenarioS();
    data.reflectedOrders = [
      {
        id: "ref-1",
        partnerId: "chefz",
        partnerName: "Chefz",
        tenantId: M1,
        merchantName: "M1",
        externalTransactionId: "x",
        orderLineId: "order:ref-1:v1",
        posSyncStatus: "Reflected",
        reflectedAt: "2026-06-05T10:00:00Z",
      },
    ];
    const entries = toReportEntries(data);
    const p = platformTotals(entries);
    expect(p.reflectionCount).toBe(1);
    // Reflection adds nothing to trial balance / margin / VAT.
    expect(trialBalance(entries).balanced).toBe(true);
    const before = platformTotals(toReportEntries(scenarioS()));
    expect(p.resaleMargin).toBe(before.resaleMargin);
    expect(p.netVatToZatca).toBe(before.netVatToZatca);
  });
});

const r2 = (n: number) => Math.round(n * 100) / 100;

describe("settlementReports — RBAC scoping (ties to org scope used by dashboards)", () => {
  const partnerCtx: OrgContext = { orgId: "org-partner-chefz", level: "Partner", partnerId: CHEFZ_PARTNER };
  const merchantCtx: OrgContext = { orgId: "org-merchant-shawarma", level: "Merchant", tenantId: TENANT_SHAWARMA };

  it("partner scope only ever yields its own entries", () => {
    const scoped = scopePortalData(structuredClone(mockPortalData), partnerCtx);
    const entries = toReportEntries(scoped);
    expect(entries.length).toBeGreaterThan(0);
    expect(entries.every((e) => e.partnerId === CHEFZ_PARTNER)).toBe(true);
  });

  it("merchant scope only ever yields its own entries (no other entity's figures)", () => {
    const scoped = scopePortalData(structuredClone(mockPortalData), merchantCtx);
    const entries = toReportEntries(scoped);
    expect(entries.length).toBeGreaterThan(0);
    expect(entries.every((e) => e.tenantId === TENANT_SHAWARMA)).toBe(true);
  });

  it("platform scope sees more entities than a single partner", () => {
    const platform = toReportEntries(mockPortalData);
    const partnerOnly = toReportEntries(scopePortalData(structuredClone(mockPortalData), partnerCtx));
    const platformPartners = new Set(platform.map((e) => e.partnerId));
    const scopedPartners = new Set(partnerOnly.map((e) => e.partnerId));
    expect(platformPartners.size).toBeGreaterThan(scopedPartners.size);
    expect(scopedPartners.size).toBe(1);
  });
});
