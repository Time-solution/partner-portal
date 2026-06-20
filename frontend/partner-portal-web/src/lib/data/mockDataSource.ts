import type { IPortalDataSource, CreateActivationInput, RegisterWebhookInput } from "./IPortalDataSource";
import { loadOrSeedData, resetToSeedData, savePersistedData } from "./mockStore";
import type {
  ActivationWorkflowState,
  AuditEntry,
  CreatePortalUserInput,
  MerchantActivation,
  MerchantActivationRow,
  PortalData,
  PortalUser,
  PortalUserRole,
  SettlementReversal,
  SubscriptionBillingPeriod,
  UpdatePortalUserInput,
  WebhookDelivery,
  WebhookEndpoint,
} from "./types";
import { invertJournal } from "./types";
import { notifyPortalDataChanged } from "./portalDataEvents";
import { canGrantRole, roleRequiresPartner, type PortalRole } from "@/lib/rbac/portalRoles";

const delay = (ms = 80) => new Promise((r) => setTimeout(r, ms));

function uid(prefix: string) {
  return `${prefix}-${crypto.randomUUID().slice(0, 8)}`;
}

function maskSecret(prefix: string) {
  return `${prefix}_••••••••${Math.random().toString(36).slice(2, 6)}`;
}

function generateSecret(prefix: string) {
  return `${prefix}_${crypto.randomUUID().replace(/-/g, "")}`;
}

function recalcKpis(data: PortalData) {
  data.kpis = {
    totalPartners: data.partners.filter((p) => p.status === "Active").length,
    activeActivations: data.activations.filter((a) => a.status === "Active").length,
    settlementTotalSar: data.settlementCases
      .filter((c) => !c.reversesSettlementCaseId)
      .reduce((s, c) => s + c.journal.totalDebits.amount, 0),
    reflectedOrdersCount: data.reflectedOrders.length,
    subscriptionFeesMtd: data.billingPeriods
      .filter((b) => b.status === "Invoiced")
      .reduce((s, b) => s + b.feeInclusive.amount, 0),
  };
}

function appendAudit(data: PortalData, entry: Omit<AuditEntry, "id" | "at">) {
  data.auditLog.unshift({
    id: uid("aud"),
    at: new Date().toISOString(),
    ...entry,
  });
}

function workflowFor(data: PortalData, activationId: string): ActivationWorkflowState {
  let wf = data.activationWorkflows.find((w) => w.activationId === activationId);
  if (!wf) {
    wf = { activationId, stage: "Pending" };
    data.activationWorkflows.push(wf);
  }
  return wf;
}

function row(data: PortalData, activation: MerchantActivation): MerchantActivationRow {
  return { activation, workflow: workflowFor(data, activation.id) };
}

export class MockPortalDataSource implements IPortalDataSource {
  private data: PortalData;

  constructor() {
    this.data = loadOrSeedData();
    recalcKpis(this.data);
  }

  private persist() {
    recalcKpis(this.data);
    savePersistedData(this.data);
    notifyPortalDataChanged();
  }

  async getDashboardKpis() {
    await delay();
    return { ...this.data.kpis };
  }

  async getPartners() {
    await delay();
    return [...this.data.partners];
  }

  async getPartner(id: string) {
    await delay();
    return this.data.partners.find((p) => p.id === id);
  }

  async getCatalogItems(partnerId?: string) {
    await delay();
    return partnerId
      ? this.data.catalogItems.filter((i) => i.partnerId === partnerId)
      : [...this.data.catalogItems];
  }

  async getActivations(partnerId?: string) {
    await delay();
    const list = partnerId
      ? this.data.activations.filter((a) => a.partnerId === partnerId)
      : this.data.activations;
    return list.map((a) => row(this.data, a));
  }

  async getSettlementCases(partnerId?: string) {
    await delay();
    const cases = this.data.settlementCases.filter((c) => !c.reversesSettlementCaseId);
    return partnerId ? cases.filter((c) => c.partnerId === partnerId) : [...cases];
  }

  async getReversals(partnerId?: string) {
    await delay();
    return partnerId
      ? this.data.reversals.filter((r) => r.partnerId === partnerId)
      : [...this.data.reversals];
  }

  async getReflectedOrders(partnerId?: string) {
    await delay();
    return partnerId
      ? this.data.reflectedOrders.filter((o) => o.partnerId === partnerId)
      : [...this.data.reflectedOrders];
  }

  async getBillingPeriods(partnerId?: string) {
    await delay();
    return partnerId
      ? this.data.billingPeriods.filter((b) => b.partnerId === partnerId)
      : [...this.data.billingPeriods];
  }

  async getWebhookEndpoints(partnerId?: string) {
    await delay();
    return partnerId
      ? this.data.webhookEndpoints.filter((w) => w.partnerId === partnerId)
      : [...this.data.webhookEndpoints];
  }

  async getWebhookDeliveries(endpointId?: string) {
    await delay();
    return endpointId
      ? this.data.webhookDeliveries.filter((d) => d.endpointId === endpointId)
      : [...this.data.webhookDeliveries];
  }

  async getPartnerCredentials(partnerId?: string) {
    await delay();
    return partnerId
      ? this.data.credentials.filter((c) => c.partnerId === partnerId)
      : [...this.data.credentials];
  }

  async getTeams() {
    await delay();
    return [...this.data.teams];
  }

  async getAuditLog() {
    await delay();
    return [...this.data.auditLog];
  }

  async listUsers() {
    await delay();
    return [...this.data.users].sort((a, b) => a.name.localeCompare(b.name));
  }

  async createUser(input: CreatePortalUserInput) {
    await delay();
    const name = input.name.trim();
    const email = normalizeEmail(input.email);
    if (!name) throw new Error("Name is required");
    if (!isValidEmail(email)) throw new Error("Invalid email address");
    if (this.data.users.some((u) => normalizeEmail(u.email) === email)) {
      throw new Error("A user with this email already exists");
    }
    assertCanGrantRole(input.role);
    if (roleRequiresPartner(input.role) && !input.partnerId) {
      throw new Error("Partner assignment is required for Partner Finance");
    }
    if (!roleRequiresPartner(input.role) && input.partnerId) {
      throw new Error("Partner assignment applies only to Partner Finance");
    }
    const partner = input.partnerId ? resolvePartner(this.data, input.partnerId) : undefined;
    const user: PortalUser = {
      id: uid("user"),
      name,
      email,
      roles: [input.role],
      partnerId: partner?.id,
      partnerName: partner ? (partner.tradeName ?? partner.legalName) : undefined,
      status: "Invited",
      invitedAt: new Date().toISOString(),
    };
    this.data.users.unshift(user);
    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: "PlatformAdmin",
      action: "Invited user (mock — no email sent)",
      target: user.email,
    });
    this.persist();
    return { ...user };
  }

  async updateUser(id: string, input: UpdatePortalUserInput) {
    await delay();
    const user = this.data.users.find((u) => u.id === id);
    if (!user) throw new Error("User not found");

    if (input.name !== undefined) {
      const name = input.name.trim();
      if (!name) throw new Error("Name is required");
      user.name = name;
    }

    if (input.email !== undefined) {
      const email = normalizeEmail(input.email);
      if (!isValidEmail(email)) throw new Error("Invalid email address");
      if (this.data.users.some((u) => u.id !== id && normalizeEmail(u.email) === email)) {
        throw new Error("A user with this email already exists");
      }
      user.email = email;
    }

    if (input.role !== undefined) {
      assertCanGrantRole(input.role);
      user.roles = [input.role];
      if (!roleRequiresPartner(input.role)) {
        user.partnerId = undefined;
        user.partnerName = undefined;
      }
    }

    const effectiveRole = user.roles[0]!;
    if (input.partnerId !== undefined) {
      if (input.partnerId === null || input.partnerId === "") {
        if (roleRequiresPartner(effectiveRole)) {
          throw new Error("Partner assignment is required for Partner Finance");
        }
        user.partnerId = undefined;
        user.partnerName = undefined;
      } else {
        if (!roleRequiresPartner(effectiveRole)) {
          throw new Error("Partner assignment applies only to Partner Finance");
        }
        const partner = resolvePartner(this.data, input.partnerId);
        user.partnerId = partner.id;
        user.partnerName = partner.tradeName ?? partner.legalName;
      }
    } else if (roleRequiresPartner(effectiveRole) && !user.partnerId) {
      throw new Error("Partner assignment is required for Partner Finance");
    }

    if (input.status !== undefined) {
      user.status = input.status;
    }

    if (input.resendInvite) {
      user.status = "Invited";
      user.invitedAt = new Date().toISOString();
      appendAudit(this.data, {
        actor: actorNameSafe(),
        role: "PlatformAdmin",
        action: "Resent invite (mock — no email sent)",
        target: user.email,
      });
    }

    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: "PlatformAdmin",
      action: "Updated user",
      target: user.email,
    });
    this.persist();
    return { ...user };
  }

  async getAll() {
    await delay();
    return structuredClone(this.data);
  }

  async createActivation(input: CreateActivationInput) {
    await delay();
    const partner = this.data.partners.find((p) => p.id === input.partnerId);
    if (!partner) throw new Error("Partner not found");
    if (partner.status !== "Active") throw new Error("Partner is not available for activation");

    const duplicate = this.data.activations.find(
      (a) =>
        a.partnerId === input.partnerId &&
        a.tenantId === input.tenantId &&
        a.status !== "Ended",
    );
    if (duplicate) throw new Error("Partner already activated for this merchant");

    const catalogItem = this.data.catalogItems.find(
      (c) => c.id === input.catalogItemId && c.partnerId === input.partnerId && c.status === "Active",
    );
    if (!catalogItem) throw new Error("Catalog item not found");

    const activation: MerchantActivation = {
      id: uid("act"),
      partnerId: input.partnerId,
      tenantId: input.tenantId,
      merchantName: input.merchantName,
      catalogItemId: catalogItem.id,
      catalogItemName: catalogItem.name,
      resalePrice: { ...catalogItem.partnerCost },
      status: "Pending",
      idempotencyKey: `preview-${input.partnerId}-${input.tenantId}`,
    };
    this.data.activations.push(activation);
    workflowFor(this.data, activation.id);
    appendAudit(this.data, {
      actor: input.merchantName,
      role: "MerchantPreview",
      action: "Requested partner activation (merchant preview)",
      target: activation.id,
    });
    this.persist();
    return row(this.data, activation);
  }

  async endActivation(activationId: string) {
    await delay();
    const activation = this.data.activations.find((a) => a.id === activationId);
    if (!activation) throw new Error("Activation not found");
    if (activation.status === "Ended") throw new Error("Activation already ended");
    activation.status = "Ended";
    activation.endedAt = new Date().toISOString();
    const wf = workflowFor(this.data, activationId);
    wf.stage = "Ended";
    appendAudit(this.data, {
      actor: activation.merchantName,
      role: "MerchantPreview",
      action: "Ended partner activation",
      target: activationId,
    });
    this.persist();
    return row(this.data, activation);
  }

  async requestActivation(activationId: string, actorName: string) {
    await delay();
    const activation = this.data.activations.find((a) => a.id === activationId);
    if (!activation) throw new Error("Activation not found");
    const wf = workflowFor(this.data, activationId);
    if (wf.stage !== "Pending") throw new Error("Invalid workflow transition");
    wf.stage = "PsmRequested";
    wf.psmRequestedBy = actorName;
    appendAudit(this.data, {
      actor: actorName,
      role: "PartnerSuccessManager",
      action: "Requested activation",
      target: activationId,
    });
    this.persist();
    return row(this.data, activation);
  }

  async setActivationTerms(activationId: string, actorName: string) {
    await delay();
    const activation = this.data.activations.find((a) => a.id === activationId);
    if (!activation) throw new Error("Activation not found");
    const wf = workflowFor(this.data, activationId);
    if (wf.stage !== "PsmRequested") throw new Error("Invalid workflow transition");
    wf.stage = "AccountantTermsSet";
    wf.accountantTermsBy = actorName;
    appendAudit(this.data, {
      actor: actorName,
      role: "Accountant",
      action: "Set billing terms",
      target: activationId,
    });
    this.persist();
    return row(this.data, activation);
  }

  async approveActivation(activationId: string, actorName: string) {
    await delay();
    const activation = this.data.activations.find((a) => a.id === activationId);
    if (!activation) throw new Error("Activation not found");
    const wf = workflowFor(this.data, activationId);
    if (wf.stage !== "AccountantTermsSet") throw new Error("Invalid workflow transition");
    wf.adminApprovedBy = actorName;
    wf.stage = "Active";
    activation.status = "Active";
    activation.activatedAt = new Date().toISOString();
    wf.notificationSent = true;
    appendAudit(this.data, {
      actor: actorName,
      role: "PlatformAdmin",
      action: "Approved activation + sent notification",
      target: activationId,
    });
    this.persist();
    return row(this.data, activation);
  }

  async registerWebhook(input: RegisterWebhookInput) {
    await delay();
    const partner = this.data.partners.find((p) => p.id === input.partnerId);
    if (!partner) throw new Error("Partner not found");
    const secretOnce = generateSecret("whsec");
    const endpoint: WebhookEndpoint = {
      id: uid("wh"),
      partnerId: input.partnerId,
      partnerName: partner.tradeName ?? partner.legalName,
      url: input.url,
      eventTypes: input.eventTypes,
      status: "Active",
      secretHint: maskSecret("whsec"),
      createdAt: new Date().toISOString(),
    };
    this.data.webhookEndpoints.push(endpoint);
    appendAudit(this.data, {
      actor: "Portal user",
      role: "PlatformAdmin",
      action: "Registered webhook",
      target: endpoint.id,
    });
    this.persist();
    return { ...endpoint, secretOnce };
  }

  async setWebhookPaused(endpointId: string, paused: boolean) {
    await delay();
    const endpoint = this.data.webhookEndpoints.find((w) => w.id === endpointId);
    if (!endpoint) throw new Error("Webhook not found");
    endpoint.status = paused ? "Paused" : "Active";
    appendAudit(this.data, {
      actor: "Portal user",
      role: "PlatformAdmin",
      action: paused ? "Paused webhook" : "Resumed webhook",
      target: endpointId,
    });
    this.persist();
    return { ...endpoint };
  }

  async rotateWebhookSecret(endpointId: string) {
    await delay();
    const endpoint = this.data.webhookEndpoints.find((w) => w.id === endpointId);
    if (!endpoint) throw new Error("Webhook not found");
    const secretOnce = generateSecret("whsec");
    endpoint.secretHint = maskSecret("whsec");
    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: "PlatformAdmin",
      action: "Rotated webhook secret",
      target: endpointId,
    });
    this.persist();
    return { ...endpoint, secretOnce };
  }

  async retryWebhookDelivery(deliveryId: string) {
    await delay();
    const delivery = this.data.webhookDeliveries.find((d) => d.id === deliveryId);
    if (!delivery) throw new Error("Delivery not found");
    delivery.status = "Retrying";
    delivery.attemptCount += 1;
    delivery.updatedAt = new Date().toISOString();
    setTimeout(() => {
      delivery.status = "Delivered";
      delivery.responseCode = 200;
      delivery.updatedAt = new Date().toISOString();
      this.persist();
    }, 1500);
    this.persist();
    return { ...delivery };
  }

  async simulateWebhookDelivery(endpointId: string) {
    await delay();
    const endpoint = this.data.webhookEndpoints.find((w) => w.id === endpointId);
    if (!endpoint) throw new Error("Webhook not found");
    if (endpoint.status === "Paused") throw new Error("Endpoint is paused");
    const delivery: WebhookDelivery = {
      id: uid("del"),
      endpointId,
      partnerId: endpoint.partnerId,
      eventType: endpoint.eventTypes[0] ?? "ping",
      status: "Delivered",
      attemptCount: 1,
      responseCode: 200,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    endpoint.lastDeliveryAt = delivery.updatedAt;
    this.data.webhookDeliveries.unshift(delivery);
    this.persist();
    return delivery;
  }

  async rotateClientSecret(partnerId: string) {
    await delay();
    const cred = this.data.credentials.find((c) => c.partnerId === partnerId);
    if (!cred) throw new Error("Credential not found");
    const secretOnce = generateSecret("m2m");
    cred.secretHint = maskSecret("m2m");
    cred.lastRotatedAt = new Date().toISOString();
    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: "PlatformAdmin",
      action: "Rotated client secret",
      target: partnerId,
    });
    this.persist();
    return { ...cred, secretOnce };
  }

  async triggerReversal(settlementCaseId: string) {
    await delay();
    const original = this.data.settlementCases.find((c) => c.id === settlementCaseId);
    if (!original) throw new Error("Settlement case not found");
    if (this.data.reversals.some((r) => r.originalCaseId === settlementCaseId)) {
      throw new Error("Reversal already exists");
    }
    const reversal: SettlementReversal = {
      id: uid("stl-rev"),
      originalCaseId: settlementCaseId,
      partnerId: original.partnerId,
      partnerName: original.partnerName,
      orderLineId: "",
      journal: invertJournal(original.journal),
      netsToZero: true,
      createdAt: new Date().toISOString(),
    };
    this.data.reversals.unshift(reversal);
    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: "Accountant",
      action: "Triggered reversal",
      target: settlementCaseId,
    });
    this.persist();
    return reversal;
  }

  async generateInvoice(periodId: string) {
    await delay();
    const period = this.data.billingPeriods.find((p) => p.id === periodId);
    if (!period) throw new Error("Billing period not found");
    if (period.status === "Invoiced") throw new Error("Already invoiced");
    period.status = "Invoiced";
    period.invoiceNumber = `INV-${period.periodKey}-${String(Math.floor(Math.random() * 9000) + 1000)}`;
    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: "Accountant",
      action: "Generated invoice (BETA)",
      target: periodId,
    });
    this.persist();
    return { ...period };
  }

  async advanceBillingPeriod(partnerId: string) {
    await delay();
    const partnerPeriods = this.data.billingPeriods
      .filter((p) => p.partnerId === partnerId)
      .sort((a, b) => b.periodKey.localeCompare(a.periodKey));
    const latest = partnerPeriods[0];
    const [y, m] = (latest?.periodKey ?? "2026-06").split("-").map(Number);
    const nextDate = new Date(y, m, 1);
    const periodKey = `${nextDate.getFullYear()}-${String(nextDate.getMonth() + 1).padStart(2, "0")}`;
    if (this.data.billingPeriods.some((p) => p.partnerId === partnerId && p.periodKey === periodKey)) {
      throw new Error("Period already exists");
    }
    const partner = this.data.partners.find((p) => p.id === partnerId);
    const period: SubscriptionBillingPeriod = {
      id: uid("bill"),
      partnerId,
      merchantName: latest?.merchantName ?? partner?.tradeName ?? "Merchant",
      periodKey,
      feeInclusive: { amount: 115, currency: "SAR", vatInclusive: true },
      outputVat: 15,
      netFee: 100,
      billingChargeId: uid("chg"),
      journalBalanced: true,
      status: "Charged",
    };
    this.data.billingPeriods.unshift(period);
    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: "Accountant",
      action: "Advanced billing period",
      target: period.id,
    });
    this.persist();
    return period;
  }

  async resetDemoData() {
    await delay();
    this.data = resetToSeedData();
    recalcKpis(this.data);
    notifyPortalDataChanged();
  }
}

function actorNameSafe() {
  return "Portal user";
}

function normalizeEmail(email: string) {
  return email.trim().toLowerCase();
}

function isValidEmail(email: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
}

function resolvePartner(data: PortalData, partnerId: string) {
  const partner = data.partners.find((p) => p.id === partnerId);
  if (!partner) throw new Error("Partner not found");
  return partner;
}

function assertCanGrantRole(targetRole: PortalUserRole) {
  const grantor: PortalRole = "PlatformAdmin";
  if (!canGrantRole(grantor, targetRole)) {
    throw new Error("Cannot assign a role higher than your own");
  }
}

let instance: MockPortalDataSource | null = null;

export function getMockPortalDataSource(): MockPortalDataSource {
  instance ??= new MockPortalDataSource();
  return instance;
}

export function resetMockPortalDataSourceInstance() {
  instance = null;
}
