/**
 * SINGLE-SOURCE settlement reporting (mock).
 *
 * Every figure in the Reports tab and the dashboard drill-down is derived HERE,
 * from one mock dataset (`PortalData`) that has already been scoped by the viewer
 * (via `getScopedData(ctx)` / `MockPortalDataSource.view()`). Nothing is recomputed
 * per dashboard, so a partner's view ties exactly to the accountant's view of that
 * partner — they are literally the same numbers filtered to a different entity.
 *
 * Financial figures come ONLY from balanced journal lines:
 *   - settlement cases (Principal resale / COD-Online collection),
 *   - reversals,
 *   - subscription billing periods (synthesised into a balanced fee journal).
 * ReflectionOnly orders carry NO money — they contribute a count only (never to
 * the trial balance, margin, or VAT), mirroring the backend Phase-C semantics.
 */
import type {
  PortalData,
  SettlementCase,
  SettlementJournalLine,
  SubscriptionBillingPeriod,
} from "@/lib/data/types";

const r2 = (n: number) => Math.round(n * 100) / 100;

/**
 * VAT rate — single source for frontend math. Backend authoritative source is VatMath /
 * FinanceVatOptions. THE ONLY place this literal is allowed to live on the frontend; every other
 * module imports it from here rather than re-typing 0.15 / 1.15.
 */
export const VAT_RATE = 0.15;

/** Round to 2dp, away-from-zero (matches the backend money policy; identical to half-up for positives). */
function round2AwayFromZero(n: number): number {
  return ((n < 0 ? -1 : 1) * Math.round(Math.abs(n) * 100)) / 100;
}

/**
 * Split a VAT-inclusive amount into ex-VAT + VAT, round-per-line (2dp, away-from-zero).
 * THE single home for the inclusive→net+VAT formula — every call site (resale, collection,
 * four-way split, activation fee preview) routes here so the math exists in exactly one place.
 */
export function splitInclusiveVat(
  amountInclusive: number,
  rate: number = VAT_RATE,
): { exVat: number; vat: number } {
  const exVat = round2AwayFromZero(amountInclusive / (1 + rate));
  return { exVat, vat: round2AwayFromZero(amountInclusive - exVat) };
}

/** Reporting period as a calendar month key, "YYYY-MM". */
export type Period = string;

export function periodOf(isoOrKey: string): Period {
  return (isoOrKey ?? "").slice(0, 7);
}

/** How Zahy participated in the order this entry represents. */
export type EntryMode = "Principal" | "Collection" | "SubscriptionFee" | "Reversal" | "ReflectionOnly";

/** One reporting entry — a tagged set of (balanced) journal lines for one order/invoice. */
export interface ReportEntry {
  orderRef: string;
  partnerId: string;
  partnerName: string;
  tenantId?: string;
  merchantName?: string;
  period: Period;
  mode: EntryMode;
  /** True for journal-bearing entries; false for ReflectionOnly (count only). */
  financial: boolean;
  lines: SettlementJournalLine[];
}

/** Account codes used by the frontend mock journals (names, not numeric codes). */
export const ReportAccount = {
  MerchantReceivable: "MerchantReceivable",
  MerchantPayable: "MerchantPayable",
  RevenueNetSell: "RevenueNetSell",
  FeeRevenue: "FeeRevenue",
  VatOutput: "VatOutput",
  VatInput: "VatInput",
  PartnerCost: "PartnerCost",
  PartnerPayable: "PartnerPayable",
  CollectionsReceivable: "CollectionsReceivable",
  Cash: "Cash",
} as const;

export interface AccountBalance {
  account: string;
  debit: number;
  credit: number;
  /** debit − credit (positive = net debit balance). */
  net: number;
}

export interface TrialBalance {
  period: Period | "all";
  accounts: AccountBalance[];
  totalDebits: number;
  totalCredits: number;
  balanced: boolean;
}

export interface PlatformTotals {
  period: Period | "all";
  resaleMargin: number;
  feeRevenue: number;
  outputVat: number;
  inputVat: number;
  netVatToZatca: number;
  collectedTotal: number;
  reflectionCount: number;
  orderCount: number;
}

export interface VatControlReport {
  period: Period | "all";
  /** Account 2200 (output VAT) movements. */
  outputVat: number;
  /** Account 1300 (input VAT) movements. */
  inputVat: number;
  /** Net VAT payable to ZATCA = 2200 − 1300. */
  netVatToZatca: number;
}

export interface PartnerStatement {
  partnerId: string;
  partnerName: string;
  period: Period | "all";
  /** Net of account 2100 movements (credit − debit) = what Zahy owes the partner. */
  payable: number;
  orderCount: number;
  reflectionCount: number;
  entries: ReportEntry[];
}

export interface MerchantStatement {
  tenantId: string;
  merchantName: string;
  period: Period | "all";
  /** Net of account 1200 movements (debit − credit) = what the merchant owes Zahy. */
  receivable: number;
  /** Subscription / per-txn fee revenue (account 4200). */
  fees: number;
  orderCount: number;
  reflectionCount: number;
  entries: ReportEntry[];
}

export interface OrderGroupReport {
  period: Period;
  accounts: AccountBalance[];
  totalDebits: number;
  totalCredits: number;
  orderCount: number;
}

/* ------------------------------------------------------------------ */
/* Entry construction (the one place the dataset becomes report rows)  */
/* ------------------------------------------------------------------ */

/** Synthesise the balanced fee journal for a subscription billing period. */
function feeJournalLines(p: SubscriptionBillingPeriod): SettlementJournalLine[] {
  const currency = p.feeInclusive.currency;
  const money = (amount: number) => ({ amount: r2(amount), currency, vatInclusive: false });
  return [
    { account: ReportAccount.MerchantReceivable, direction: "Debit", amount: money(p.feeInclusive.amount), entry: "Fee" },
    { account: ReportAccount.FeeRevenue, direction: "Credit", amount: money(p.netFee), entry: "Fee" },
    { account: ReportAccount.VatOutput, direction: "Credit", amount: money(p.outputVat), entry: "Fee" },
  ];
}

function caseMode(c: SettlementCase): EntryMode {
  if (c.reversesSettlementCaseId) return "Reversal";
  return c.collection ? "Collection" : "Principal";
}

/**
 * Turn a (scoped) dataset into report entries. This is the SINGLE source —
 * filtered views (period / partner / merchant) are pure slices of this array.
 */
export function toReportEntries(data: PortalData): ReportEntry[] {
  const entries: ReportEntry[] = [];

  for (const c of data.settlementCases) {
    entries.push({
      orderRef: c.externalTransactionId,
      partnerId: c.partnerId,
      partnerName: c.partnerName,
      tenantId: c.tenantId,
      merchantName: data.activations.find((a) => a.tenantId === c.tenantId)?.merchantName,
      period: periodOf(c.createdAt),
      mode: caseMode(c),
      financial: true,
      lines: c.journal.lines,
    });
  }

  for (const rv of data.reversals) {
    entries.push({
      orderRef: rv.orderLineId,
      partnerId: rv.partnerId,
      partnerName: rv.partnerName,
      tenantId: rv.tenantId,
      period: periodOf(rv.createdAt),
      mode: "Reversal",
      financial: true,
      lines: rv.journal.lines,
    });
  }

  for (const p of data.billingPeriods) {
    entries.push({
      orderRef: p.invoiceNumber ?? p.billingChargeId,
      partnerId: p.partnerId,
      partnerName: data.partners.find((x) => x.id === p.partnerId)?.tradeName ?? "",
      tenantId: p.tenantId,
      merchantName: p.merchantName,
      period: periodOf(p.periodKey),
      mode: "SubscriptionFee",
      financial: true,
      lines: feeJournalLines(p),
    });
  }

  for (const o of data.reflectedOrders) {
    entries.push({
      orderRef: o.orderLineId,
      partnerId: o.partnerId,
      partnerName: o.partnerName,
      tenantId: o.tenantId,
      merchantName: o.merchantName,
      period: periodOf(o.reflectedAt),
      mode: "ReflectionOnly",
      financial: false,
      lines: [],
    });
  }

  return entries;
}

/* ------------------------------------------------------------------ */
/* Filters (pure slices — never recompute)                            */
/* ------------------------------------------------------------------ */

export function listPeriods(entries: ReportEntry[]): Period[] {
  return Array.from(new Set(entries.map((e) => e.period).filter(Boolean))).sort().reverse();
}

export function inPeriod(entries: ReportEntry[], period?: Period): ReportEntry[] {
  return period ? entries.filter((e) => e.period === period) : entries;
}

const financial = (entries: ReportEntry[]) => entries.filter((e) => e.financial);

/* ------------------------------------------------------------------ */
/* Aggregations                                                        */
/* ------------------------------------------------------------------ */

function sumLine(entries: ReportEntry[], account: string, direction: "Debit" | "Credit"): number {
  let total = 0;
  for (const e of financial(entries)) {
    for (const l of e.lines) {
      if (l.account === account && l.direction === direction) total += l.amount.amount;
    }
  }
  return r2(total);
}

export function aggregateAccounts(entries: ReportEntry[]): AccountBalance[] {
  const map = new Map<string, { debit: number; credit: number }>();
  for (const e of financial(entries)) {
    for (const l of e.lines) {
      const row = map.get(l.account) ?? { debit: 0, credit: 0 };
      if (l.direction === "Debit") row.debit += l.amount.amount;
      else row.credit += l.amount.amount;
      map.set(l.account, row);
    }
  }
  return Array.from(map.entries())
    .map(([account, v]) => ({ account, debit: r2(v.debit), credit: r2(v.credit), net: r2(v.debit - v.credit) }))
    .sort((a, b) => a.account.localeCompare(b.account));
}

export function trialBalance(entries: ReportEntry[], period: Period | "all" = "all"): TrialBalance {
  const accounts = aggregateAccounts(entries);
  const totalDebits = r2(accounts.reduce((s, a) => s + a.debit, 0));
  const totalCredits = r2(accounts.reduce((s, a) => s + a.credit, 0));
  return { period, accounts, totalDebits, totalCredits, balanced: totalDebits === totalCredits };
}

export function platformTotals(entries: ReportEntry[], period: Period | "all" = "all"): PlatformTotals {
  const A = ReportAccount;
  const outputVat = sumLine(entries, A.VatOutput, "Credit");
  const inputVat = sumLine(entries, A.VatInput, "Debit");
  const resaleMargin = r2(sumLine(entries, A.RevenueNetSell, "Credit") - sumLine(entries, A.PartnerCost, "Debit"));
  const feeRevenue = sumLine(entries, A.FeeRevenue, "Credit");
  const collectedTotal = financial(entries)
    .filter((e) => e.mode === "Collection")
    .reduce((s, e) => {
      const items = e.lines.find((l) => l.account === A.MerchantPayable && l.direction === "Credit")?.amount.amount ?? 0;
      const cash = e.lines.find(
        (l) => (l.account === A.CollectionsReceivable || l.account === A.Cash) && l.direction === "Debit" && l.entry === "Collection",
      )?.amount.amount ?? 0;
      return s + (cash || items);
    }, 0);
  return {
    period,
    resaleMargin,
    feeRevenue,
    outputVat,
    inputVat,
    netVatToZatca: r2(outputVat - inputVat),
    collectedTotal: r2(collectedTotal),
    reflectionCount: entries.filter((e) => e.mode === "ReflectionOnly").length,
    orderCount: new Set(financial(entries).map((e) => e.orderRef)).size,
  };
}

/**
 * VAT control read model — net VAT to ZATCA = account 2200 (output) − 1300 (input).
 * This is the SINGLE source for the net-VAT figure; never a flat 15% of any total.
 */
export function vatControl(entries: ReportEntry[], period: Period | "all" = "all"): VatControlReport {
  const outputVat = sumLine(entries, ReportAccount.VatOutput, "Credit");
  const inputVat = sumLine(entries, ReportAccount.VatInput, "Debit");
  return { period, outputVat, inputVat, netVatToZatca: r2(outputVat - inputVat) };
}

export interface JournalSummary {
  outputVat: number;
  inputVat: number;
  netVatToZatca: number;
  /** Clean resale margin = RevenueNetSell (credit) − PartnerCost (debit). */
  resaleMargin: number;
  feeRevenue: number;
}

/**
 * Per-journal summary of the figures this module OWNS — net VAT (2200−1300 ⇒ VatOutput−VatInput),
 * resale margin (4100−5100 ⇒ RevenueNetSell−PartnerCost) and fee revenue (4200 ⇒ FeeRevenue).
 * The SINGLE source for a one-journal view: `deriveSettlementSummary` and the analytics trend read
 * these here instead of recomputing them, so per-case figures tie exactly to the platform totals.
 */
export function summarizeJournalLines(lines: SettlementJournalLine[]): JournalSummary {
  const entries: ReportEntry[] = [
    { orderRef: "", partnerId: "", partnerName: "", period: "", mode: "Principal", financial: true, lines },
  ];
  const outputVat = sumLine(entries, ReportAccount.VatOutput, "Credit");
  const inputVat = sumLine(entries, ReportAccount.VatInput, "Debit");
  const resaleMargin = r2(
    sumLine(entries, ReportAccount.RevenueNetSell, "Credit") - sumLine(entries, ReportAccount.PartnerCost, "Debit"),
  );
  const feeRevenue = sumLine(entries, ReportAccount.FeeRevenue, "Credit");
  return { outputVat, inputVat, netVatToZatca: r2(outputVat - inputVat), resaleMargin, feeRevenue };
}

export function partnerStatement(
  entries: ReportEntry[],
  partnerId: string,
  period: Period | "all" = "all",
): PartnerStatement {
  const own = entries.filter((e) => e.partnerId === partnerId);
  const payable = r2(
    sumLine(own, ReportAccount.PartnerPayable, "Credit") - sumLine(own, ReportAccount.PartnerPayable, "Debit"),
  );
  return {
    partnerId,
    partnerName: own[0]?.partnerName ?? "",
    period,
    payable,
    orderCount: new Set(financial(own).map((e) => e.orderRef)).size,
    reflectionCount: own.filter((e) => e.mode === "ReflectionOnly").length,
    entries: own,
  };
}

export function merchantStatement(
  entries: ReportEntry[],
  tenantId: string,
  period: Period | "all" = "all",
): MerchantStatement {
  const own = entries.filter((e) => e.tenantId === tenantId);
  const receivable = r2(
    sumLine(own, ReportAccount.MerchantReceivable, "Debit") - sumLine(own, ReportAccount.MerchantReceivable, "Credit"),
  );
  return {
    tenantId,
    merchantName: own.find((e) => e.merchantName)?.merchantName ?? "",
    period,
    receivable,
    fees: sumLine(own, ReportAccount.FeeRevenue, "Credit"),
    orderCount: new Set(financial(own).map((e) => e.orderRef)).size,
    reflectionCount: own.filter((e) => e.mode === "ReflectionOnly").length,
    entries: own,
  };
}

export function orderGroup(entries: ReportEntry[], period: Period): OrderGroupReport {
  const slice = inPeriod(entries, period);
  const accounts = aggregateAccounts(slice);
  return {
    period,
    accounts,
    totalDebits: r2(accounts.reduce((s, a) => s + a.debit, 0)),
    totalCredits: r2(accounts.reduce((s, a) => s + a.credit, 0)),
    orderCount: new Set(financial(slice).map((e) => e.orderRef)).size,
  };
}

/** Distinct partners that appear in the (scoped) entries. */
export function partnersIn(entries: ReportEntry[]): { partnerId: string; partnerName: string }[] {
  const map = new Map<string, string>();
  for (const e of entries) if (e.partnerId) map.set(e.partnerId, e.partnerName);
  return Array.from(map.entries()).map(([partnerId, partnerName]) => ({ partnerId, partnerName }));
}

/** Distinct merchants that appear in the (scoped) entries. */
export function merchantsIn(entries: ReportEntry[]): { tenantId: string; merchantName: string }[] {
  const map = new Map<string, string>();
  for (const e of entries) if (e.tenantId) map.set(e.tenantId, e.merchantName ?? "");
  return Array.from(map.entries()).map(([tenantId, merchantName]) => ({ tenantId, merchantName }));
}
