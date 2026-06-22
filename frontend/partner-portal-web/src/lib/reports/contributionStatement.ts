/**
 * PLATFORM trading CONTRIBUTION / P&L (mock, BETA) — what Zahy nets on trading.
 *
 * NOT a full operating P&L: payroll / rent / G&A live in the ERP and are out of scope.
 *
 * Single source — every figure is READ from the existing settlement report read models
 * (`aggregateAccounts`, `platformTotals`, `vatControl`); nothing is recomputed and
 * `settlementReports.ts` math is untouched. We only sum already-derived account balances:
 *
 *   SALES (revenue)      = Resale Revenue 4100 + Fee Revenue 4200
 *   − COST OF SALES      = Partner Cost / COGS 5100
 *   = GROSS CONTRIBUTION  (sales − cost of sales)   ← ties to platformTotals.resaleMargin + feeRevenue
 *   − NET VAT (to ZATCA) = 2200 − 1300              ← PERIOD-based (see grain rule)
 *   = ZAHY NET CONTRIBUTION
 *
 * GRAIN (matches the A-fix): SALES + COST + GROSS are derived from the DAY-scoped entries
 * (true day-to-day); NET VAT is derived from the PERIOD/month-overlap entries (never day-sliced,
 * because output VAT mixes day-precise settlements with month-only subscriptions and net VAT is a
 * filing-period concept). Caller passes both grains from `scopeReportEntries`.
 */
import {
  aggregateAccounts,
  ReportAccount,
  vatControl,
  type ReportEntry,
} from "@/lib/reports/settlementReports";

const r2 = (n: number) => Math.round(n * 100) / 100;

export interface ContributionStatement {
  /** Resale Revenue 4100 (RevenueNetSell credit). */
  salesResale: number;
  /** Fee Revenue 4200 (FeeRevenue credit). */
  salesFee: number;
  /** SALES TOTAL = 4100 + 4200. */
  salesTotal: number;
  /** COST OF SALES = Partner Cost / COGS 5100 (PartnerCost debit) — the PURCHASE total. */
  costOfSales: number;
  /** GROSS CONTRIBUTION = sales − cost of sales (== platformTotals.resaleMargin + feeRevenue). */
  grossContribution: number;
  /** NET VAT to ZATCA = 2200 − 1300, PERIOD-based (never day-sliced). */
  netVatToZatca: number;
  /** ZAHY NET CONTRIBUTION = gross contribution − net VAT. */
  netContribution: number;
}

/**
 * Assemble the platform contribution/P&L from already-derived report read models.
 * @param dayEntries day-to-day scoped entries (sales / purchase / gross contribution).
 * @param vatEntries period/month-overlap scoped entries (net VAT only — filing-period figure).
 */
export function buildContributionStatement(
  dayEntries: ReportEntry[],
  vatEntries: ReportEntry[],
): ContributionStatement {
  const accounts = aggregateAccounts(dayEntries);
  const credit = (account: string) => accounts.find((a) => a.account === account)?.credit ?? 0;
  const debit = (account: string) => accounts.find((a) => a.account === account)?.debit ?? 0;

  const salesResale = r2(credit(ReportAccount.RevenueNetSell));
  const salesFee = r2(credit(ReportAccount.FeeRevenue));
  const salesTotal = r2(salesResale + salesFee);
  const costOfSales = r2(debit(ReportAccount.PartnerCost));
  const grossContribution = r2(salesTotal - costOfSales);

  // Net VAT stays a PERIOD/filing figure — read from the month-overlap entries, never day-sliced.
  const netVatToZatca = vatControl(vatEntries).netVatToZatca;
  const netContribution = r2(grossContribution - netVatToZatca);

  return {
    salesResale,
    salesFee,
    salesTotal,
    costOfSales,
    grossContribution,
    netVatToZatca,
    netContribution,
  };
}
