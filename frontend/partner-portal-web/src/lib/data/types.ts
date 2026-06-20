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
  offeringKind: OfferingKind;
  participationMode: ParticipationMode;
  partnerCost: Money;
  settlementBook: "Marketplace" | "Integration";
  status: "Draft" | "Active" | "Archived";
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
}

/** Journal shape — matches backend SettlementJournalReadDto. */
export interface SettlementJournal {
  currency: string;
  postedAt?: string;
  totalDebits: Money;
  totalCredits: Money;
  lines: SettlementJournalLine[];
}

/** Settlement case — journal nested, not flattened buy/sell/margin. */
export interface SettlementCase {
  id: string;
  partnerId: string;
  partnerName: string;
  externalTransactionId: string;
  book: "Marketplace" | "Integration";
  state: SettlementCaseState;
  journal: SettlementJournal;
  createdAt: string;
  reversesSettlementCaseId?: string;
}

export interface SettlementReversal {
  id: string;
  originalCaseId: string;
  partnerId: string;
  partnerName: string;
  orderLineId: string;
  journal: SettlementJournal;
  netsToZero: boolean;
  createdAt: string;
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
}

export interface SubscriptionBillingPeriod {
  id: string;
  partnerId: string;
  merchantName: string;
  periodKey: string;
  feeInclusive: Money;
  outputVat: number;
  netFee: number;
  billingChargeId: string;
  invoiceNumber?: string;
  journalBalanced: boolean;
  status: "Charged" | "Invoiced";
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
  webhookEndpoints: WebhookEndpoint[];
  webhookDeliveries: WebhookDelivery[];
  credentials: PartnerCredential[];
  kpis: DashboardKpis;
  teams: PortalTeam[];
  auditLog: AuditEntry[];
  users: PortalUser[];
}

/** Derive display summary from journal lines (Principal delivery pattern). */
export function deriveSettlementSummary(journal: SettlementJournal) {
  const lineAmount = (account: string, direction: JournalDirection) =>
    journal.lines.find((l) => l.account === account && l.direction === direction)?.amount.amount ?? 0;
  const buy = lineAmount("DeliveryCost", "Credit");
  const collected = lineAmount("AggregatorClearing", "Debit");
  const margin = lineAmount("ShippingMarginRevenue", "Credit");
  const vatOut = lineAmount("VatOutput", "Credit");
  const vatIn = lineAmount("VatInput", "Debit");
  return {
    buyPrice: { amount: buy, currency: journal.currency, vatInclusive: true },
    sellPrice: { amount: collected, currency: journal.currency, vatInclusive: true },
    margin,
    outputVat: vatOut,
    inputVat: vatIn,
    netVatToZatca: Math.round((vatOut - vatIn) * 100) / 100,
  };
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
