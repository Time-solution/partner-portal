import type { Org, OrgLevel, OrgUser } from "@/lib/org/orgModel";
import { VAT_RATE, splitInclusiveVat, summarizeJournalLines } from "@/lib/reports/settlementReports";

export type { Org, OrgLevel, OrgUser } from "@/lib/org/orgModel";

export type PartnerType =
  | "Marketplace"
  | "Service"
  | "Carrier"
  | "Aggregator"
  | "ThreePL";

export type PartnerStatus = "Pending" | "Active" | "Suspended" | "Closed";

export type OfferingKind =
  | "ServiceOneOff"
  | "ServiceSubscription"
  | "DeliveryFulfilmentPerOrder"
  | "FnBItemsPerSale";

export type ParticipationMode = "Principal" | "ReflectionOnly" | "SubscriptionFee";

/** Persisted activation status — matches backend GET shape. */
export type ActivationStatus = "Pending" | "Active" | "Suspended" | "Ended";

/** View-only workflow overlay — not part of persisted entity. */
export type ActivationWorkflowStage =
  | "Pending"
  | "PsmRequested"
  | "AccountantTermsSet"
  | "AdminApproved"
  | "Active"
  | "Suspended"
  | "Ended";

export type SettlementCaseState = "Collected" | "Allocated" | "Invoiced" | "Cleared";

export type JournalDirection = "Debit" | "Credit";

export interface Money {
  amount: number;
  currency: string;
  vatInclusive: boolean;
}

export interface Partner {
  id: string;
  legalName: string;
  tradeName?: string;
  type: PartnerType;
  status: PartnerStatus;
  primaryContactEmail: string;
  participationMode: ParticipationMode;
  accentClass: string;
  /**
   * Display-only mock label for ReflectionOnly Commerce vs F&B split.
   * Maps to real OfferingKind/field in production — not a backend entity field.
   */
  marketplaceCategory?: "commerce" | "fnb";
  /** Parked business module (Consignment / FBA demo). */
  parkedModule?: "consignment";
}

export interface PartnerCatalogItem {
  id: string;
  partnerId: string;
  code: string;
  name: string;
  description?: string;
  offeringKind: OfferingKind;
  participationMode: ParticipationMode;
  partnerCost: Money;
  settlementBook: "Marketplace" | "Integration";
  status: "Draft" | "Active" | "Archived";
}

/** Which side of an activation is billed for a fee line. */
export type ActivationFeePayer = "Merchant" | "Partner";

/** One configurable activation fee line (VAT-inclusive amount + payer). */
export interface ActivationFeeLine {
  enabled: boolean;
  /** Amount the payer is charged, VAT-inclusive (e.g. 40 / month, 1 / txn). */
  amountInclusive: Money;
  payer: ActivationFeePayer;
}

/**
 * Per-activation fee matrix — config + DISPLAY ONLY (mock). Both lines may be ON at
 * once and each may bill a different side. NEVER posts a journal (the fee journal
 * stays OFF in the backend); the UI shows a fee preview (ex-VAT + VAT) only.
 */
export interface ActivationFeeConfig {
  /** Recurring monthly subscription fee. */
  subscription: ActivationFeeLine;
  /** Per successful transaction fee. */
  perTransaction: ActivationFeeLine;
}

/** Persisted merchant activation — matches backend GET. */
export interface MerchantActivation {
  id: string;
  partnerId: string;
  tenantId: string;
  merchantName: string;
  catalogItemId: string;
  catalogItemName: string;
  resalePrice: Money;
  status: ActivationStatus;
  activatedAt?: string;
  endedAt?: string;
  idempotencyKey?: string;
  externalReference?: string;
  /** Mock activation fee matrix (config + display only — no journal posted). */
  fees?: ActivationFeeConfig;
}

/** View-only workflow state keyed by activation id. */
export interface ActivationWorkflowState {
  activationId: string;
  stage: ActivationWorkflowStage;
  psmRequestedBy?: string;
  accountantTermsBy?: string;
  adminApprovedBy?: string;
  notificationSent?: boolean;
}

export interface MerchantActivationRow {
  activation: MerchantActivation;
  workflow: ActivationWorkflowState;
}

export interface SettlementJournalLine {
  account: string;
  direction: JournalDirection;
  amount: Money;
  /** Optional grouping for multi-entry journals (e.g. "Sell" / "Buy" resale entries). */
  entry?: string;
}

/** Journal shape — matches backend SettlementJournalReadDto. */
export interface SettlementJournal {
  currency: string;
  postedAt?: string;
  totalDebits: Money;
  totalCredits: Money;
  lines: SettlementJournalLine[];
}

/* ------------------------------------------------------------------ */
/* Money cycle (Prompt 2/5): collection + remittance + 3-way split     */
/* ------------------------------------------------------------------ */

/** Who physically collects the customer's payment. */
export type CollectedBy = "DeliveryCompany" | "Gateway";

/** A fee withheld before remittance (delivery-company commission / gateway fee). */
export interface SettlementDeduction {
  label: string;
  amount: Money;
  kind: "Flat" | "Percent";
  /** Percentage rate when kind === "Percent" (e.g. 8.5). */
  rate?: number;
}

/**
 * The FOUR-way split of what the customer paid (3 parties + tax).
 *
 * Invariant on every order:  Collected = Merchant + Delivery + ZahyMargin + NetVAT
 *   merchant         — items passed through to the merchant (no Zahy VAT on these).
 *   deliveryCompany  — the delivery/partner gross cost (their full take, VAT-inclusive).
 *   zahy             — Zahy's NET margin (net sell − net buy on its resale leg).
 *   zatca            — Net VAT remitted to ZATCA (Output VAT − Input VAT). The 4th recipient.
 *
 * Order 901 (worked case): 113.00 = 100.00 + 10.00 + 2.60 + 0.40.
 */
export interface FourWaySplit {
  merchant: Money;
  deliveryCompany: Money;
  zahy: Money;
  /** ZATCA / Net VAT recipient — the slice the previous 3-way split was short by. */
  zatca: Money;
}

/** @deprecated 3-way split was short by net VAT — use {@link FourWaySplit}. */
export type ThreeWaySplit = FourWaySplit;

/**
 * Collection + remittance facts for an order/settlement.
 *
 *   COD:    delivery company collects totalCollected → deducts its fee → remits net to Zahy.
 *           Money flows delivery company → Zahy; Zahy then pays merchant + keeps its share.
 *   Online: gateway/Zahy collects totalCollected → pays delivery + merchant, keeps its share.
 *           Money flows Zahy → out.
 */
export interface CollectionRemittance {
  paymentMethod: Extract<PaymentMethod, "COD" | "Online">;
  collectedBy: CollectedBy;
  totalCollected: Money;
  deductions: SettlementDeduction[];
  /** COD: collected − deductions remitted to Zahy. Online: amount Zahy pays out (merchant + delivery). */
  netRemitted: Money;
  split: FourWaySplit;
  /** Gateway seam — empty until a real gateway is wired. */
  gatewayReference?: string;
  gatewayStatus?: string;
}

/** Settlement case — journal nested, not flattened buy/sell/margin. */
export interface SettlementCase {
  id: string;
  partnerId: string;
  partnerName: string;
  /** Merchant party of the bridged activation — drives shared-flow visibility. */
  tenantId?: string;
  externalTransactionId: string;
  book: "Marketplace" | "Integration";
  state: SettlementCaseState;
  journal: SettlementJournal;
  createdAt: string;
  reversesSettlementCaseId?: string;
  /** Money cycle — collection/remittance + 3-way split (COD/Online). */
  collection?: CollectionRemittance;
}

export interface SettlementReversal {
  id: string;
  originalCaseId: string;
  partnerId: string;
  partnerName: string;
  /** Inherited from the original case's activation party. */
  tenantId?: string;
  orderLineId: string;
  journal: SettlementJournal;
  netsToZero: boolean;
  createdAt: string;
}

/**
 * Reflection display facts for a ReflectionOnly order (e.g. Chefz). DISPLAY ONLY —
 * no Zahy money moves. Shows the full marketplace flow so finance/POS can see the
 * picture: merchant menu price → partner list price → delivery → customer paid, plus
 * Zahy's own activation fee shown SEPARATELY (the only Zahy revenue, from the fee config).
 */
export interface ReflectionDetail {
  /** Merchant's own menu price for the item. */
  merchantMenuPrice: Money;
  /** Price listed on the partner's app/marketplace. */
  partnerListPrice: Money;
  /** Delivery fee on the partner app. */
  deliveryFee: Money;
  /** Total the customer paid on the partner app (list + delivery). */
  customerPaid: Money;
  /** Zahy's activation fee for this txn, VAT-inclusive (display only — from fee config). */
  zahyFeeInclusive?: Money;
  /** Whether this counts as a successful (billable) transaction for the period. */
  successful: boolean;
}

export interface ReflectedPartnerOrder {
  id: string;
  partnerId: string;
  partnerName: string;
  tenantId: string;
  merchantName: string;
  externalTransactionId: string;
  orderLineId: string;
  posSyncStatus: "Reflected" | "PendingPosPoll" | "Acknowledged";
  reflectedAt: string;
  /** ReflectionOnly process detail (display only). */
  reflection?: ReflectionDetail;
}

/* ------------------------------------------------------------------ */
/* Money cycle (Prompt 1/5): payment + receipt foundation              */
/* Mirrors real entity shapes so the backend can wire it later.        */
/* ------------------------------------------------------------------ */

export type PaymentMethod = "COD" | "Online" | "Transfer";

export type PaymentStatus = "Pending" | "Paid" | "Partial" | "Overdue";

/** A single payment applied to an invoice (partial payments => multiple records). */
export interface InvoicePayment {
  id: string;
  amount: Money;
  /** ISO date the payment was received. */
  date: string;
  method: PaymentMethod;
  /** Bank/gateway/COD reference (free text). */
  reference: string;
}

/** Proof of payment — SEPARATE from the invoice (invoice = request; receipt = proof paid). */
export interface Receipt {
  id: string;
  /** Clean, user-facing receipt number (e.g. ZR-2026-0001). */
  receiptNo: string;
  /** The invoice this receipt proves payment against. */
  invoiceRef: string;
  /** Source billing period id (mock join key). */
  billingPeriodId: string;
  partnerId: string;
  tenantId?: string;
  merchantName: string;
  amount: Money;
  date: string;
  method: PaymentMethod;
  sent: boolean;
  sentAt?: string;
}

export interface SubscriptionBillingPeriod {
  id: string;
  partnerId: string;
  /** Merchant party of the bridged activation — drives shared-flow visibility. */
  tenantId?: string;
  merchantName: string;
  periodKey: string;
  feeInclusive: Money;
  outputVat: number;
  netFee: number;
  billingChargeId: string;
  invoiceNumber?: string;
  journalBalanced: boolean;
  status: "Charged" | "Invoiced";
  /** Money cycle — due date drives Overdue status. */
  dueDate?: string;
  /** Money cycle — current payment state (recomputed when a payment is recorded). */
  paymentStatus?: PaymentStatus;
  /** Money cycle — applied payments (supports partials). */
  payments?: InvoicePayment[];
}

export type WebhookEndpointStatus = "Active" | "Paused";

export type WebhookDeliveryStatus = "Delivered" | "Retrying" | "Dlq";

export interface WebhookEndpoint {
  id: string;
  partnerId: string;
  partnerName: string;
  url: string;
  eventTypes: string[];
  status: WebhookEndpointStatus;
  /** Masked reference only — never store raw secret after creation. */
  secretHint: string;
  createdAt: string;
  lastDeliveryAt?: string;
}

export interface WebhookDelivery {
  id: string;
  endpointId: string;
  partnerId: string;
  eventType: string;
  status: WebhookDeliveryStatus;
  attemptCount: number;
  responseCode?: number;
  createdAt: string;
  updatedAt: string;
}

export interface PartnerCredential {
  partnerId: string;
  partnerName: string;
  clientId: string;
  /** Masked after rotation — raw shown once only in UI state. */
  secretHint: string;
  scopes: string[];
  tokenEndpointUrl: string;
  lastRotatedAt?: string;
}

export interface DashboardKpis {
  totalPartners: number;
  activeActivations: number;
  settlementTotalSar: number;
  reflectedOrdersCount: number;
  subscriptionFeesMtd: number;
}

export interface PortalTeam {
  id: string;
  name: string;
  kind: "Core" | "PartnerSuccess";
  memberCount: number;
}

export interface AuditEntry {
  id: string;
  at: string;
  actor: string;
  role: string;
  action: string;
  target: string;
}

/** Mirrors ABP Identity user shape for live-swap. */
export type PortalUserRole =
  | "PlatformAdmin"
  | "Accountant"
  | "PartnerSuccessManager"
  | "PartnerFinance";

export type PortalUserStatus = "Active" | "Invited" | "Suspended";

export interface PortalUser {
  id: string;
  name: string;
  email: string;
  roles: PortalUserRole[];
  partnerId?: string;
  partnerName?: string;
  status: PortalUserStatus;
  invitedAt?: string;
  lastLoginAt?: string;
}

export interface CreatePortalUserInput {
  name: string;
  email: string;
  role: PortalUserRole;
  partnerId?: string;
}

export interface UpdatePortalUserInput {
  name?: string;
  email?: string;
  role?: PortalUserRole;
  partnerId?: string | null;
  status?: PortalUserStatus;
  resendInvite?: boolean;
}

export interface PortalData {
  partners: Partner[];
  catalogItems: PartnerCatalogItem[];
  activations: MerchantActivation[];
  activationWorkflows: ActivationWorkflowState[];
  settlementCases: SettlementCase[];
  reversals: SettlementReversal[];
  reflectedOrders: ReflectedPartnerOrder[];
  billingPeriods: SubscriptionBillingPeriod[];
  /** Money cycle — proof-of-payment records issued when a payment is recorded. */
  receipts: Receipt[];
  webhookEndpoints: WebhookEndpoint[];
  webhookDeliveries: WebhookDelivery[];
  credentials: PartnerCredential[];
  kpis: DashboardKpis;
  teams: PortalTeam[];
  auditLog: AuditEntry[];
  users: PortalUser[];
  /** Multi-org accounts (Platform / Partner / Merchant) + their self-managed users. */
  orgs: Org[];
  orgUsers: OrgUser[];
}

/** Central-admin creates a Partner or Merchant org account after approval. */
export interface CreateOrgAccountInput {
  level: Exclude<OrgLevel, "Platform">;
  name: string;
  /** Required for Partner orgs — the approved Partner.Id. */
  partnerId?: string;
  /** Required for Merchant orgs — the merchant TenantId. */
  tenantId?: string;
  /** Optional first admin user for the new org. */
  adminName?: string;
  adminEmail?: string;
}

export interface CreateOrgUserInput {
  name: string;
  email: string;
  rolePreset: string;
  /** Optional per-user overrides on top of the preset (defaults to preset permissions). */
  permissions?: string[];
}

export interface UpdateOrgUserInput {
  name?: string;
  email?: string;
  rolePreset?: string;
  permissions?: string[];
  status?: OrgUser["status"];
}

/** Demo-account hint surfaced on the mock login screen. */
export interface LoginAccount {
  email: string;
  name: string;
  orgName: string;
  level: OrgLevel;
  rolePreset: string;
}

/**
 * Derive display summary from journal lines.
 *
 * Margin and net VAT are DERIVED here (never booked as journal lines that would
 * inflate the total). Supports both the clean resale journal (MerchantReceivable /
 * PartnerPayable / RevenueNetSell / PartnerCost) and the legacy / collection accounts
 * (AggregatorClearing / DeliveryCost / ShippingMarginRevenue).
 */
export function deriveSettlementSummary(journal: SettlementJournal) {
  const round = (n: number) => Math.round(n * 100) / 100;
  const lineAmount = (account: string, direction: JournalDirection) =>
    journal.lines.find((l) => l.account === account && l.direction === direction)?.amount.amount ?? 0;

  // Net VAT (2200−1300) and resale margin (4100−5100) come from the SINGLE source
  // (settlementReports.summarizeJournalLines) — never recomputed here.
  const owned = summarizeJournalLines(journal.lines);

  const netRevenue = lineAmount("RevenueNetSell", "Credit");
  const netCost = lineAmount("PartnerCost", "Debit");

  // Sell/buy GROSS = net + VAT for the clean resale/collection delivery leg (presentation shape);
  // legacy/collection-clearing journals fall back to their clearing accounts.
  const sell = netRevenue
    ? round(netRevenue + owned.outputVat)
    : lineAmount("MerchantReceivable", "Debit") || lineAmount("AggregatorClearing", "Debit");
  const buy = netCost
    ? round(netCost + owned.inputVat)
    : lineAmount("PartnerPayable", "Credit") || lineAmount("DeliveryCost", "Credit");

  // Clean resale margin from the single source; legacy journals fall back to their margin line.
  const margin =
    netRevenue || netCost ? owned.resaleMargin : lineAmount("ShippingMarginRevenue", "Credit");
  return {
    buyPrice: { amount: buy, currency: journal.currency, vatInclusive: true },
    sellPrice: { amount: sell, currency: journal.currency, vatInclusive: true },
    margin,
    outputVat: owned.outputVat,
    inputVat: owned.inputVat,
    netVatToZatca: owned.netVatToZatca,
  };
}

/**
 * Build a clean Principal resale journal — NET amounts + VAT control lines, no
 * gross/VAT double-presentation. Two separate balanced entries:
 *
 *   SELL (merchant pays sellGross): DR MerchantReceivable = CR RevenueNetSell + Output VAT
 *   BUY  (Zahy owes partner buyGross): DR PartnerCost + Input VAT = CR PartnerPayable
 *
 * Headline total = the sell side (sellGross). Margin and net VAT are derived elsewhere.
 */
export function buildResaleJournal(opts: {
  sellGross: number;
  buyGross: number;
  vatRate?: number;
  currency?: string;
  postedAt?: string;
}): SettlementJournal {
  const { sellGross, buyGross, vatRate = VAT_RATE, currency = "SAR", postedAt } = opts;
  const r2 = (n: number) => Math.round(n * 100) / 100;
  const money = (amount: number): Money => ({ amount: r2(amount), currency, vatInclusive: false });

  const { exVat: sellNet, vat: outputVat } = splitInclusiveVat(sellGross, vatRate);
  const { exVat: buyNet, vat: inputVat } = splitInclusiveVat(buyGross, vatRate);

  const lines: SettlementJournalLine[] = [
    { account: "MerchantReceivable", direction: "Debit", amount: money(sellGross), entry: "Sell" },
    { account: "RevenueNetSell", direction: "Credit", amount: money(sellNet), entry: "Sell" },
    { account: "VatOutput", direction: "Credit", amount: money(outputVat), entry: "Sell" },
    { account: "PartnerCost", direction: "Debit", amount: money(buyNet), entry: "Buy" },
    { account: "VatInput", direction: "Debit", amount: money(inputVat), entry: "Buy" },
    { account: "PartnerPayable", direction: "Credit", amount: money(buyGross), entry: "Buy" },
  ];

  // Headline total = the sell side (the settlement value), not the gross of both entries.
  return {
    currency,
    postedAt,
    totalDebits: money(sellGross),
    totalCredits: money(sellGross),
    lines,
  };
}

/* ------------------------------------------------------------------ */
/* Money cycle derivations (PURE — used by UI, export, and tests)      */
/* ------------------------------------------------------------------ */

const round2 = (n: number) => Math.round(n * 100) / 100;

export function sumDeductions(deductions: SettlementDeduction[]): number {
  return round2(deductions.reduce((s, d) => s + d.amount.amount, 0));
}

/** Sum of the four split recipients: Merchant + Delivery + ZahyMargin + NetVAT. */
export function splitComponentsTotal(split: FourWaySplit): number {
  return round2(
    split.merchant.amount + split.deliveryCompany.amount + split.zahy.amount + split.zatca.amount,
  );
}

/**
 * Balance invariant — the collected total MUST go four ways (3 parties + tax):
 *   Collected = Merchant + Delivery + ZahyMargin + NetVAT.
 * Raises if the components do not equal the collected total (catches the old
 * three-way split that was short by the net VAT).
 */
export function assertSplitBalances(collected: Money, split: FourWaySplit): void {
  const sum = splitComponentsTotal(split);
  const total = round2(collected.amount);
  if (sum !== total) {
    throw new Error(
      `Order split imbalance: components ${sum.toFixed(2)} ≠ collected ${total.toFixed(2)} — ` +
        `must satisfy Collected = Merchant + Delivery + ZahyMargin + NetVAT ` +
        `(merchant ${split.merchant.amount}, delivery ${split.deliveryCompany.amount}, ` +
        `zahyMargin ${split.zahy.amount}, netVAT ${split.zatca.amount}).`,
    );
  }
}

/**
 * Derive the FOUR-way split from the real flow so the invariant holds by construction:
 *   resale leg = collected − merchantItems; net+VAT extracted from leg (sell) and partner buy.
 *   merchant = merchantItems (pass-through, no Zahy VAT)
 *   deliveryCompany = partnerBuyGross (the partner's full VAT-inclusive take)
 *   zahy = net margin (sellNet − buyNet)
 *   zatca = net VAT to ZATCA (outputVat − inputVat)
 * Proof: merchant + buyGross + (sellNet−buyNet) + (outVat−inVat) = merchant + sellNet + outVat
 *        = merchant + resaleLegGross = collected.
 */
export function deriveFourWaySplit(opts: {
  collected: number;
  merchantItems?: number;
  partnerBuyGross?: number;
  vatRate?: number;
  currency?: string;
}): FourWaySplit {
  const { collected, merchantItems = 0, partnerBuyGross = 0, vatRate = VAT_RATE, currency = "SAR" } = opts;
  const money = (amount: number): Money => ({ amount: round2(amount), currency, vatInclusive: true });

  const resaleLegGross = round2(collected - merchantItems);
  const { exVat: sellNet, vat: outputVat } = splitInclusiveVat(resaleLegGross, vatRate);
  const { exVat: buyNet, vat: inputVat } = splitInclusiveVat(partnerBuyGross, vatRate);

  return {
    merchant: money(merchantItems),
    deliveryCompany: money(partnerBuyGross),
    zahy: money(sellNet - buyNet),
    zatca: money(outputVat - inputVat),
  };
}

/** COD cash flows delivery → Zahy (inbound); Online flows Zahy → out (outbound). */
export function collectionCashFlow(c: CollectionRemittance): "Inbound" | "Outbound" {
  return c.paymentMethod === "COD" ? "Inbound" : "Outbound";
}

/**
 * Build the collection + remittance journal — grouped, balanced entries, each totalling
 * the REAL money it represents (no clearing account opened-and-closed within one entry).
 *
 *   Collection (recognition) entry — total = totalCollected (e.g. 113):
 *     items 100 → Merchant Payable (Zahy books NO items VAT — the merchant accounts for it);
 *     delivery leg → net revenue + Output VAT (the same clean net+VAT structure as the resale fix).
 *   Delivery cost entry — total = delivery buy gross (10): net cost + Input VAT (recoverable) = Partner Payable.
 *   Remittance entry — SEPARATE cash movement (COD: delivery co remits net; Online: Zahy pays out).
 *
 * Margin and net VAT are DERIVED (deriveSettlementSummary), never booked as inflating lines.
 */
export function buildCollectionJournal(
  c: CollectionRemittance,
  postedAt?: string,
  vatRate = VAT_RATE,
): SettlementJournal {
  // Reject any split that does not go four ways (Merchant + Delivery + ZahyMargin + NetVAT).
  assertSplitBalances(c.totalCollected, c.split);

  const currency = c.totalCollected.currency;
  const m = (amount: number): Money => ({ amount: round2(amount), currency, vatInclusive: false });

  const total = c.totalCollected.amount; // 113
  const items = c.split.merchant.amount; // 100 (Merchant Payable, no items VAT)
  const deliverySellGross = round2(total - items); // 13
  const deliveryBuyGross = c.split.deliveryCompany.amount; // 10

  const { exVat: sellNet, vat: outputVat } = splitInclusiveVat(deliverySellGross, vatRate); // 11.30 / 1.70
  const { exVat: buyNet, vat: inputVat } = splitInclusiveVat(deliveryBuyGross, vatRate); // 8.70 / 1.30

  // COD: delivery company collects (Zahy holds a receivable). Online: gateway/Zahy holds the cash.
  const collectionAsset = c.paymentMethod === "COD" ? "CollectionsReceivable" : "Cash";

  const lines: SettlementJournalLine[] = [
    // --- Collection recognition (total = totalCollected) ---
    { account: collectionAsset, direction: "Debit", amount: m(total), entry: "Collection" },
    { account: "MerchantPayable", direction: "Credit", amount: m(items), entry: "Collection" },
    { account: "RevenueNetSell", direction: "Credit", amount: m(sellNet), entry: "Collection" },
    { account: "VatOutput", direction: "Credit", amount: m(outputVat), entry: "Collection" },
    // --- Delivery cost (Zahy's Principal buy leg, total = buy gross) ---
    { account: "PartnerCost", direction: "Debit", amount: m(buyNet), entry: "DeliveryCost" },
    { account: "VatInput", direction: "Debit", amount: m(inputVat), entry: "DeliveryCost" },
    { account: "PartnerPayable", direction: "Credit", amount: m(deliveryBuyGross), entry: "DeliveryCost" },
  ];

  if (c.paymentMethod === "COD") {
    // Delivery company remits net (total − its retained delivery cost); the retained 10 settles its payable.
    const netRemit = round2(total - deliveryBuyGross); // 103
    lines.push(
      { account: "Cash", direction: "Debit", amount: m(netRemit), entry: "Remittance" },
      { account: "PartnerPayable", direction: "Debit", amount: m(deliveryBuyGross), entry: "Remittance" },
      { account: "CollectionsReceivable", direction: "Credit", amount: m(total), entry: "Remittance" },
    );
  } else {
    // Gateway/Zahy collected the cash; pay merchant + delivery company out.
    const payout = round2(items + deliveryBuyGross); // 110
    lines.push(
      { account: "MerchantPayable", direction: "Debit", amount: m(items), entry: "Remittance" },
      { account: "PartnerPayable", direction: "Debit", amount: m(deliveryBuyGross), entry: "Remittance" },
      { account: "Cash", direction: "Credit", amount: m(payout), entry: "Remittance" },
    );
  }

  // Headline total = the real money collected (113), not the gross of all grouped entries.
  return {
    currency,
    postedAt,
    totalDebits: m(total),
    totalCredits: m(total),
    lines,
  };
}

/** Invoice total = the VAT-inclusive fee being billed. */
export function invoiceTotal(period: SubscriptionBillingPeriod): number {
  return period.feeInclusive.amount;
}

export function sumPayments(payments: InvoicePayment[] | undefined): number {
  return round2((payments ?? []).reduce((s, p) => s + p.amount.amount, 0));
}

export interface InvoicePaymentState {
  total: number;
  amountPaid: number;
  amountOutstanding: number;
  status: PaymentStatus;
}

/**
 * Recompute the live payment state from the payments + due date.
 *   Paid    = paid >= total
 *   Partial = 0 < paid < total
 *   Overdue = unpaid/partial AND past due date
 *   Pending = otherwise
 */
export function deriveInvoicePayment(
  period: SubscriptionBillingPeriod,
  now: Date = new Date(),
): InvoicePaymentState {
  const total = round2(invoiceTotal(period));
  const amountPaid = sumPayments(period.payments);
  const amountOutstanding = round2(Math.max(total - amountPaid, 0));

  let status: PaymentStatus;
  if (amountPaid >= total && total > 0) {
    status = "Paid";
  } else {
    const overdue = !!period.dueDate && new Date(period.dueDate).getTime() < now.getTime();
    if (amountPaid > 0) status = overdue ? "Overdue" : "Partial";
    else status = overdue ? "Overdue" : "Pending";
  }
  return { total, amountPaid, amountOutstanding, status };
}

export interface MoneyBalanceRow {
  key: string;
  merchantName: string;
  partnerId: string;
  tenantId?: string;
  invoiced: number;
  paid: number;
  outstanding: number;
  overdueCount: number;
  invoiceCount: number;
}

/** Aggregate invoices into per-merchant outstanding-balance rows. */
export function deriveBalances(
  periods: SubscriptionBillingPeriod[],
  now: Date = new Date(),
): MoneyBalanceRow[] {
  const byMerchant = new Map<string, MoneyBalanceRow>();
  for (const period of periods) {
    const key = period.tenantId ?? `${period.partnerId}:${period.merchantName}`;
    const state = deriveInvoicePayment(period, now);
    const row =
      byMerchant.get(key) ??
      ({
        key,
        merchantName: period.merchantName,
        partnerId: period.partnerId,
        tenantId: period.tenantId,
        invoiced: 0,
        paid: 0,
        outstanding: 0,
        overdueCount: 0,
        invoiceCount: 0,
      } satisfies MoneyBalanceRow);
    row.invoiced = round2(row.invoiced + state.total);
    row.paid = round2(row.paid + state.amountPaid);
    row.outstanding = round2(row.outstanding + state.amountOutstanding);
    row.invoiceCount += 1;
    if (state.status === "Overdue") row.overdueCount += 1;
    byMerchant.set(key, row);
  }
  return [...byMerchant.values()].sort((a, b) => b.outstanding - a.outstanding);
}

export function invertJournal(journal: SettlementJournal): SettlementJournal {
  const lines = journal.lines.map((line) => ({
    ...line,
    direction: (line.direction === "Debit" ? "Credit" : "Debit") as JournalDirection,
    amount: { ...line.amount },
  }));
  return {
    currency: journal.currency,
    postedAt: new Date().toISOString(),
    totalDebits: { ...journal.totalCredits },
    totalCredits: { ...journal.totalDebits },
    lines,
  };
}
