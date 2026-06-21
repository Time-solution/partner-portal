import type {
  ActivationFeeConfig,
  AuditEntry,
  CreateOrgAccountInput,
  CreateOrgUserInput,
  DashboardKpis,
  LoginAccount,
  MerchantActivationRow,
  OfferingKind,
  Partner,
  PartnerCatalogItem,
  PartnerCredential,
  PaymentMethod,
  PortalData,
  Receipt,
  ReflectedPartnerOrder,
  SettlementCase,
  SettlementReversal,
  SubscriptionBillingPeriod,
  PortalTeam,
  PortalUser,
  CreatePortalUserInput,
  UpdateOrgUserInput,
  UpdatePortalUserInput,
  WebhookDelivery,
  WebhookEndpoint,
} from "./types";
import type { OrgProfile, OrgProfileScope } from "@/lib/profile/orgProfile";
import type { Org, OrgContext, OrgUser } from "@/lib/org/orgModel";

export interface RegisterWebhookInput {
  partnerId: string;
  url: string;
  eventTypes: string[];
}

export interface CreateActivationInput {
  partnerId: string;
  catalogItemId: string;
  tenantId: string;
  merchantName: string;
  /** Merchant sell / resale price — defaults to mock listed sell when omitted. */
  resalePrice?: PartnerCatalogItem["partnerCost"];
}

export interface SecretRevealResult {
  /** Shown once — never persisted in store. */
  secretOnce: string;
}

/** Money cycle — record a payment against an invoice (supports partials). */
export interface RecordPaymentInput {
  amount: number;
  /** ISO date (defaults to now in the UI). */
  date: string;
  method: PaymentMethod;
  reference: string;
}

export interface CreateCatalogItemInput {
  partnerId: string;
  code: string;
  name: string;
  description?: string;
  offeringKind: OfferingKind;
  partnerCost: PartnerCatalogItem["partnerCost"];
  participationMode?: PartnerCatalogItem["participationMode"];
  settlementBook?: PartnerCatalogItem["settlementBook"];
}

export interface UpdateCatalogItemInput {
  name: string;
  description?: string;
  partnerCost: PartnerCatalogItem["partnerCost"];
}

export interface IPortalDataSource {
  getDashboardKpis(): Promise<DashboardKpis>;
  getPartners(): Promise<Partner[]>;
  getPartner(id: string): Promise<Partner | undefined>;
  getCatalogItems(partnerId?: string): Promise<PartnerCatalogItem[]>;
  createCatalogItem(input: CreateCatalogItemInput): Promise<PartnerCatalogItem>;
  updateCatalogItem(id: string, input: UpdateCatalogItemInput): Promise<PartnerCatalogItem>;
  publishCatalogItem(id: string): Promise<PartnerCatalogItem>;
  archiveCatalogItem(id: string): Promise<PartnerCatalogItem>;
  getActivations(partnerId?: string): Promise<MerchantActivationRow[]>;
  getSettlementCases(partnerId?: string): Promise<SettlementCase[]>;
  getReversals(partnerId?: string): Promise<SettlementReversal[]>;
  getReflectedOrders(partnerId?: string): Promise<ReflectedPartnerOrder[]>;
  getBillingPeriods(partnerId?: string): Promise<SubscriptionBillingPeriod[]>;
  getWebhookEndpoints(partnerId?: string): Promise<WebhookEndpoint[]>;
  getWebhookDeliveries(endpointId?: string): Promise<WebhookDelivery[]>;
  getPartnerCredentials(partnerId?: string): Promise<PartnerCredential[]>;
  getTeams(): Promise<PortalTeam[]>;
  getAuditLog(): Promise<AuditEntry[]>;
  listUsers(): Promise<PortalUser[]>;
  createUser(input: CreatePortalUserInput): Promise<PortalUser>;
  updateUser(id: string, input: UpdatePortalUserInput): Promise<PortalUser>;
  getAll(): Promise<PortalData>;

  /* ---- Multi-org: scope-aware data + self-service users (mirrors ABP Identity) ---- */

  /** Mock email/password auth — resolves a seeded org user + its org if the demo password matches. */
  authenticate(email: string, password?: string): Promise<{ orgUser: OrgUser; org: Org } | undefined>;
  /** Demo accounts surfaced on the mock login screen. */
  listLoginAccounts(): Promise<LoginAccount[]>;
  /** Set/clear the active org scope applied to every read (login/logout/role-switch). */
  setActiveScope(ctx: OrgContext | null): void;

  /** Full data view an org is allowed to see (strict isolation + shared activation/flow bridge). */
  getScopedData(ctx: OrgContext): Promise<PortalData>;
  /** Resolve the org context for a signed-in org (by id). */
  getOrgContext(orgId: string): Promise<OrgContext | undefined>;
  /** Platform-level org directory (callers gate with Partners.View / platform level). */
  listOrgs(): Promise<Org[]>;
  /** Central-admin: create a Partner/Merchant org account after approval. */
  createOrgAccount(input: CreateOrgAccountInput): Promise<Org>;
  /** Per-org, scope-checked user list (an org admin sees ONLY its own org's users). */
  listOrgUsers(orgId: string): Promise<OrgUser[]>;
  createOrgUser(orgId: string, input: CreateOrgUserInput): Promise<OrgUser>;
  updateOrgUser(orgId: string, userId: string, input: UpdateOrgUserInput): Promise<OrgUser>;
  suspendOrgUser(orgId: string, userId: string): Promise<OrgUser>;

  /** Activation workflow — view layer transitions. */
  createActivation(input: CreateActivationInput): Promise<MerchantActivationRow>;
  endActivation(activationId: string): Promise<MerchantActivationRow>;
  /** Mock activation fee matrix update — config + display only (no journal posted). */
  setActivationFees(activationId: string, fees: ActivationFeeConfig): Promise<MerchantActivationRow>;
  requestActivation(activationId: string, actorName: string): Promise<MerchantActivationRow>;
  setActivationTerms(activationId: string, actorName: string): Promise<MerchantActivationRow>;
  approveActivation(activationId: string, actorName: string): Promise<MerchantActivationRow>;

  registerWebhook(input: RegisterWebhookInput): Promise<WebhookEndpoint & SecretRevealResult>;
  setWebhookPaused(endpointId: string, paused: boolean): Promise<WebhookEndpoint>;
  rotateWebhookSecret(endpointId: string): Promise<WebhookEndpoint & SecretRevealResult>;
  retryWebhookDelivery(deliveryId: string): Promise<WebhookDelivery>;
  simulateWebhookDelivery(endpointId: string): Promise<WebhookDelivery>;

  rotateClientSecret(partnerId: string): Promise<PartnerCredential & SecretRevealResult>;
  triggerReversal(settlementCaseId: string): Promise<SettlementReversal>;
  generateInvoice(periodId: string): Promise<SubscriptionBillingPeriod>;
  advanceBillingPeriod(partnerId: string): Promise<SubscriptionBillingPeriod>;

  /* ---- Money cycle (Prompt 1/5): payment + receipt ---- */
  getReceipts(partnerId?: string): Promise<Receipt[]>;
  /** Record a payment → updates amountPaid/status, appends to payments[], issues a receipt. */
  recordPayment(
    periodId: string,
    input: RecordPaymentInput,
  ): Promise<{ period: SubscriptionBillingPeriod; receipt: Receipt }>;
  /** Mock-send a receipt to the merchant (marks sent). */
  sendReceipt(receiptId: string): Promise<Receipt>;

  resetDemoData(): Promise<void>;

  /** Organisation profile (mock) — issuer / partner / merchant; invoice phase later. */
  getOrgProfile(scope: OrgProfileScope): Promise<OrgProfile>;
  saveOrgProfile(scope: OrgProfileScope, profile: OrgProfile): Promise<OrgProfile>;
}
