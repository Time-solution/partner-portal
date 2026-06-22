import { describe, expect, it } from "vitest";
import { createSeedData } from "@/lib/data/fixtures";
import {
  aggregateAccounts,
  platformTotals,
  ReportAccount,
  toReportEntries,
  vatControl,
} from "@/lib/reports/settlementReports";
import { scopeReportEntries } from "./reportScope";
import { buildContributionStatement } from "./contributionStatement";

const data = createSeedData();
const r2 = (n: number) => Math.round(n * 100) / 100;

// Fixture settlement dates (createdAt): 7001=06-18, 7002=06-10, wthere-svc=06-20, 7003=07-05.

describe("buildContributionStatement — ties to existing read models (no recompute)", () => {
  it("gross contribution = sales(4100+4200) − cost of sales(5100) = platformTotals.margin + fee", () => {
    const { entries, vatEntries } = scopeReportEntries(data, { mode: "period", period: "2026-06" });
    const c = buildContributionStatement(entries, vatEntries);

    // SALES + COST tie to the aggregated account balances (the existing read model).
    const accounts = aggregateAccounts(entries);
    const credit = (a: string) => accounts.find((x) => x.account === a)?.credit ?? 0;
    const debit = (a: string) => accounts.find((x) => x.account === a)?.debit ?? 0;
    expect(c.salesResale).toBe(r2(credit(ReportAccount.RevenueNetSell)));
    expect(c.salesFee).toBe(r2(credit(ReportAccount.FeeRevenue)));
    expect(c.salesTotal).toBe(r2(c.salesResale + c.salesFee));
    expect(c.costOfSales).toBe(r2(debit(ReportAccount.PartnerCost)));

    // GROSS = sales − cost AND equals platformTotals.resaleMargin + feeRevenue (no recompute).
    expect(c.grossContribution).toBe(r2(c.salesTotal - c.costOfSales));
    const p = platformTotals(entries, "all");
    expect(c.grossContribution).toBe(r2(p.resaleMargin + p.feeRevenue));
  });

  it("net contribution = gross − net VAT (period figure)", () => {
    const { entries, vatEntries } = scopeReportEntries(data, { mode: "period", period: "2026-06" });
    const c = buildContributionStatement(entries, vatEntries);
    expect(c.netVatToZatca).toBe(vatControl(vatEntries).netVatToZatca);
    expect(c.netContribution).toBe(r2(c.grossContribution - c.netVatToZatca));
  });
});

describe("buildContributionStatement — grain (matches A-fix)", () => {
  it("sales/purchase/gross are day-to-day; a 06-21+ case is excluded from a 06-05..06-19 window", () => {
    const inWindow = scopeReportEntries(data, { mode: "range", from: "2026-06-05", to: "2026-06-19" });
    const wider = scopeReportEntries(data, { mode: "range", from: "2026-06-05", to: "2026-06-30" });
    const cIn = buildContributionStatement(inWindow.entries, inWindow.vatEntries);
    const cWide = buildContributionStatement(wider.entries, wider.vatEntries);
    // The 06-20 case (wthere) lands in the wider window only → wider sales >= narrow sales.
    expect(cWide.salesTotal).toBeGreaterThanOrEqual(cIn.salesTotal);
    expect(cWide.grossContribution).not.toBe(cIn.grossContribution);
  });

  it("multi-month range AGGREGATES gross contribution across months (no month dropped)", () => {
    const june = scopeReportEntries(data, { mode: "period", period: "2026-06" });
    const july = scopeReportEntries(data, { mode: "period", period: "2026-07" });
    const both = scopeReportEntries(data, { mode: "range", from: "2026-06-01", to: "2026-07-31" });
    const cJune = buildContributionStatement(june.entries, june.vatEntries);
    const cJuly = buildContributionStatement(july.entries, july.vatEntries);
    const cBoth = buildContributionStatement(both.entries, both.vatEntries);
    expect(cBoth.grossContribution).toBe(r2(cJune.grossContribution + cJuly.grossContribution));
  });

  it("net VAT line stays period-based — a single-day pick yields the whole touched month's net VAT", () => {
    const dayPick = scopeReportEntries(data, { mode: "day", day: "2026-06-20" });
    const monthPick = scopeReportEntries(data, { mode: "period", period: "2026-06" });
    const cDay = buildContributionStatement(dayPick.entries, dayPick.vatEntries);
    const cMonth = buildContributionStatement(monthPick.entries, monthPick.vatEntries);
    // VAT line is NOT day-sliced (same month figure)…
    expect(cDay.netVatToZatca).toBe(cMonth.netVatToZatca);
    // …while sales/gross DID narrow to the day.
    expect(cDay.salesTotal).not.toBe(cMonth.salesTotal);
  });
});

describe("buildContributionStatement — never recomputes; reads the full entry set on 'all'", () => {
  it("equals platformTotals over the full data when given all entries", () => {
    const all = toReportEntries(data);
    const c = buildContributionStatement(all, all);
    const p = platformTotals(all, "all");
    expect(c.grossContribution).toBe(r2(p.resaleMargin + p.feeRevenue));
  });
});
