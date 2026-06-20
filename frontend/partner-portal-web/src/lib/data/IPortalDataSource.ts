import type {
  AuditEntry,
  CreateOrgAccountInput,
  CreateOrgUserInput,
  DashboardKpis,
  LoginAccount,
  MerchantActivationRow,
  Partner,
  PartnerCatalogItem,
  PartnerCredential,
  PortalData,
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
}

export interface SecretRevealResult {
  /** Shown once — never persisted in store. */
  secretOnce: string;
}

export interface IPortalDataSource {
  getDashboardKpis(): Promise<DashboardKpis>;
  getPartners(): Promise<Partner[]>;
  getPartner(id: string): Promise<Partner | undefined>;
  getCatalogItems(partnerId?: string): Promise<PartnerCatalogItem[]>;
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

  /** Mock email/password auth — resolves a seeded org user + its org (password not checked). */
  authenticate(email: string): Promise<{ orgUser: OrgUser; org: Org } | undefined>;
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

  resetDemoData(): Promise<void>;
}
