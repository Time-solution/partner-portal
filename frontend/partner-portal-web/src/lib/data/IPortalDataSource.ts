import type {
  AuditEntry,
  DashboardKpis,
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
  UpdatePortalUserInput,
  WebhookDelivery,
  WebhookEndpoint,
} from "./types";

export interface RegisterWebhookInput {
  partnerId: string;
  url: string;
  eventTypes: string[];
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

  /** Activation workflow — view layer transitions. */
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
