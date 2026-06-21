import type { CreateActivationInput, IPortalDataSource, RecordPaymentInput, RegisterWebhookInput } from "./IPortalDataSource";
import { loadOrSeedData, resetToSeedData, savePersistedData } from "./mockStore";
import {
  buildMerchantActivationIdempotencyKey,
  merchantListedSellPrice,
} from "@/lib/catalog/merchantBrowse";
import type {
  ActivationFeeConfig,
  ActivationWorkflowState,
  AuditEntry,
  CreateOrgAccountInput,
  CreateOrgUserInput,
  CreatePortalUserInput,
  LoginAccount,
  MerchantActivation,
  MerchantActivationRow,
  PortalData,
  PortalUser,
  PortalUserRole,
  Receipt,
  SettlementReversal,
  SubscriptionBillingPeriod,
  UpdateOrgUserInput,
  UpdatePortalUserInput,
  WebhookDelivery,
  WebhookEndpoint,
} from "./types";
import { deriveInvoicePayment, invertJournal } from "./types";
import { notifyPortalDataChanged } from "./portalDataEvents";
import { canGrantRole, roleRequiresPartner, type PortalRole } from "@/lib/rbac/portalRoles";
import {
  orgContextFromOrg,
  presetPermissions,
  presetsForLevel,
  sanitizePermissionsForLevel,
  type Org,
  type OrgContext,
  type OrgUser,
} from "@/lib/org/orgModel";
import { scopePortalData } from "@/lib/org/orgScope";
import {
  readOrgProfile,
  resetOrgProfiles,
  writeOrgProfile,
} from "@/lib/profile/orgProfileStore";
import type { OrgProfile, OrgProfileScope } from "@/lib/profile/orgProfile";

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
  /** Active org scope — when set, every READ is filtered through the proven scope engine. */
  private activeScope: OrgContext | null = null;

  constructor() {
    this.data = loadOrSeedData();
    recalcKpis(this.data);
  }

  private persist() {
    recalcKpis(this.data);
    savePersistedData(this.data);
    notifyPortalDataChanged();
  }

  /** Set/clear the org scope applied to all reads (login sets it, logout clears it). */
  setActiveScope(ctx: OrgContext | null) {
    this.activeScope = ctx;
  }

  /** The scoped data view for the current org (strict isolation + shared activation bridge). */
  private view(): PortalData {
    return this.activeScope ? scopePortalData(this.data, this.activeScope) : this.data;
  }

  async getDashboardKpis() {
    await delay();
    return { ...this.view().kpis };
  }

  async getPartners() {
    await delay();
    return [...this.view().partners];
  }

  async getPartner(id: string) {
    await delay();
    return this.view().partners.find((p) => p.id === id);
  }

  async getCatalogItems(partnerId?: string) {
    await delay();
    const items = this.view().catalogItems;
    return partnerId ? items.filter((i) => i.partnerId === partnerId) : [...items];
  }

  async createCatalogItem(input: import("./IPortalDataSource").CreateCatalogItemInput) {
    await delay();
    const duplicate = this.data.catalogItems.some(
      (i) => i.partnerId === input.partnerId && i.code === input.code.trim(),
    );
    if (duplicate) {
      throw new Error("Duplicate catalog code for partner.");
    }
    const item = {
      id: uid("cat"),
      partnerId: input.partnerId,
      code: input.code.trim(),
      name: input.name.trim(),
      description: input.description?.trim(),
      offeringKind: input.offeringKind,
      participationMode: input.participationMode ?? "Principal",
      partnerCost: input.partnerCost,
      settlementBook: input.settlementBook ?? "Integration",
      status: "Draft" as const,
    };
    this.data.catalogItems.push(item);
    this.persist();
    return { ...item };
  }

  async updateCatalogItem(id: string, input: import("./IPortalDataSource").UpdateCatalogItemInput) {
    await delay();
    const item = this.data.catalogItems.find((i) => i.id === id);
    if (!item) throw new Error("Catalog item not found.");
    if (item.status !== "Draft") throw new Error("Only draft items can be updated.");
    item.name = input.name.trim();
    item.description = input.description?.trim();
    item.partnerCost = input.partnerCost;
    this.persist();
    return { ...item };
  }

  async publishCatalogItem(id: string) {
    await delay();
    const item = this.data.catalogItems.find((i) => i.id === id);
    if (!item) throw new Error("Catalog item not found.");
    item.status = "Active";
    this.persist();
    return { ...item };
  }

  async archiveCatalogItem(id: string) {
    await delay();
    const item = this.data.catalogItems.find((i) => i.id === id);
    if (!item) throw new Error("Catalog item not found.");
    item.status = "Archived";
    this.persist();
    return { ...item };
  }

  async getActivations(partnerId?: string) {
    await delay();
    const view = this.view();
    const list = partnerId
      ? view.activations.filter((a) => a.partnerId === partnerId)
      : view.activations;
    return list.map((a) => row(view, a));
  }

  async getSettlementCases(partnerId?: string) {
    await delay();
    const cases = this.view().settlementCases.filter((c) => !c.reversesSettlementCaseId);
    return partnerId ? cases.filter((c) => c.partnerId === partnerId) : [...cases];
  }

  async getReversals(partnerId?: string) {
    await delay();
    const items = this.view().reversals;
    return partnerId ? items.filter((r) => r.partnerId === partnerId) : [...items];
  }

  async getReflectedOrders(partnerId?: string) {
    await delay();
    const items = this.view().reflectedOrders;
    return partnerId ? items.filter((o) => o.partnerId === partnerId) : [...items];
  }

  async getBillingPeriods(partnerId?: string) {
    await delay();
    const items = this.view().billingPeriods;
    return partnerId ? items.filter((b) => b.partnerId === partnerId) : [...items];
  }

  async getReceipts(partnerId?: string) {
    await delay();
    const items = this.view().receipts;
    return partnerId ? items.filter((r) => r.partnerId === partnerId) : [...items];
  }

  async getWebhookEndpoints(partnerId?: string) {
    await delay();
    const items = this.view().webhookEndpoints;
    return partnerId ? items.filter((w) => w.partnerId === partnerId) : [...items];
  }

  async getWebhookDeliveries(endpointId?: string) {
    await delay();
    const items = this.view().webhookDeliveries;
    return endpointId ? items.filter((d) => d.endpointId === endpointId) : [...items];
  }

  async getPartnerCredentials(partnerId?: string) {
    await delay();
    const items = this.view().credentials;
    return partnerId ? items.filter((c) => c.partnerId === partnerId) : [...items];
  }

  async getTeams() {
    await delay();
    return [...this.view().teams];
  }

  async getAuditLog() {
    await delay();
    return [...this.view().auditLog];
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
    return structuredClone(this.view());
  }

  /* ---- Multi-org: scope-aware data + self-service users ---- */

  async getScopedData(ctx: OrgContext) {
    await delay();
    return scopePortalData(structuredClone(this.data), ctx);
  }

  /** Mock email/password auth — matches a seeded org user against its demo password. */
  async authenticate(email: string, password?: string) {
    await delay();
    const normalized = normalizeEmail(email);
    const user = this.data.orgUsers.find((u) => normalizeEmail(u.email) === normalized);
    if (!user || user.status === "Suspended") return undefined;
    // MOCK demo credential check (localStorage only). If a seed password is set, it must match.
    if (user.password && user.password !== (password ?? "")) return undefined;
    const org = this.data.orgs.find((o) => o.id === user.orgId);
    if (!org || org.status === "Suspended") return undefined;
    return {
      orgUser: { ...user, permissions: [...user.permissions] },
      org: { ...org },
    };
  }

  /** Demo-account hint list shown on the login screen (active users only). */
  async listLoginAccounts(): Promise<LoginAccount[]> {
    await delay();
    return this.data.orgUsers
      .filter((u) => u.status !== "Suspended")
      .map((u) => {
        const org = this.data.orgs.find((o) => o.id === u.orgId);
        return {
          email: u.email,
          name: u.name,
          orgName: org?.name ?? u.orgId,
          level: org?.level ?? "Platform",
          rolePreset: u.rolePreset,
        };
      })
      .sort((a, b) => a.level.localeCompare(b.level) || a.name.localeCompare(b.name));
  }

  async getOrgContext(orgId: string) {
    await delay();
    const org = this.data.orgs.find((o) => o.id === orgId);
    return org ? orgContextFromOrg(org) : undefined;
  }

  async listOrgs() {
    await delay();
    return [...this.data.orgs];
  }

  async createOrgAccount(input: CreateOrgAccountInput) {
    await delay();
    const name = input.name.trim();
    if (!name) throw new Error("Organization name is required");
    if (input.level === "Partner") {
      if (!input.partnerId) throw new Error("partnerId is required for a Partner org");
      if (this.data.orgs.some((o) => o.level === "Partner" && o.partnerId === input.partnerId)) {
        throw new Error("This partner already has an org account");
      }
    }
    if (input.level === "Merchant") {
      if (!input.tenantId) throw new Error("tenantId is required for a Merchant org");
      if (this.data.orgs.some((o) => o.level === "Merchant" && o.tenantId === input.tenantId)) {
        throw new Error("This merchant already has an org account");
      }
    }

    const org: Org = {
      id: uid("org"),
      level: input.level,
      name,
      partnerId: input.level === "Partner" ? input.partnerId : undefined,
      tenantId: input.level === "Merchant" ? input.tenantId : undefined,
      status: "Active",
      createdAt: new Date().toISOString(),
    };
    this.data.orgs.push(org);

    // Seed the org's first admin user so it can self-manage from day one.
    const adminPreset = input.level === "Partner" ? "PartnerAdmin" : "MerchantAdmin";
    const adminEmail = normalizeEmail(input.adminEmail ?? `admin@${slug(name)}.sa`);
    if (isValidEmail(adminEmail) && !this.data.orgUsers.some((u) => normalizeEmail(u.email) === adminEmail)) {
      this.data.orgUsers.push({
        id: uid("ou"),
        orgId: org.id,
        name: (input.adminName ?? "Org Admin").trim() || "Org Admin",
        email: adminEmail,
        rolePreset: adminPreset,
        permissions: presetPermissions(input.level, adminPreset),
        status: "Invited",
        invitedAt: new Date().toISOString(),
      });
    }

    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: "PlatformAdmin",
      action: `Created ${input.level} org account (mock)`,
      target: org.id,
    });
    this.persist();
    return { ...org };
  }

  async listOrgUsers(orgId: string) {
    await delay();
    this.assertOrgExists(orgId);
    return this.data.orgUsers
      .filter((u) => u.orgId === orgId)
      .map((u) => ({ ...u, permissions: [...u.permissions] }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  async createOrgUser(orgId: string, input: CreateOrgUserInput) {
    await delay();
    const org = this.assertOrgExists(orgId);
    const name = input.name.trim();
    const email = normalizeEmail(input.email);
    if (!name) throw new Error("Name is required");
    if (!isValidEmail(email)) throw new Error("Invalid email address");
    if (this.data.orgUsers.some((u) => normalizeEmail(u.email) === email)) {
      throw new Error("A user with this email already exists");
    }
    this.assertPresetForOrg(org, input.rolePreset);
    const permissions = sanitizePermissionsForLevel(
      org.level,
      input.permissions ?? presetPermissions(org.level, input.rolePreset),
    );
    const user: OrgUser = {
      id: uid("ou"),
      orgId,
      name,
      email,
      rolePreset: input.rolePreset,
      permissions,
      status: "Invited",
      invitedAt: new Date().toISOString(),
    };
    this.data.orgUsers.push(user);
    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: org.level,
      action: "Invited org user (mock — no email sent)",
      target: `${org.name}: ${user.email}`,
    });
    this.persist();
    return { ...user, permissions: [...user.permissions] };
  }

  async updateOrgUser(orgId: string, userId: string, input: UpdateOrgUserInput) {
    await delay();
    const org = this.assertOrgExists(orgId);
    const user = this.data.orgUsers.find((u) => u.id === userId && u.orgId === orgId);
    if (!user) throw new Error("User not found in this organization");

    if (input.name !== undefined) {
      const name = input.name.trim();
      if (!name) throw new Error("Name is required");
      user.name = name;
    }
    if (input.email !== undefined) {
      const email = normalizeEmail(input.email);
      if (!isValidEmail(email)) throw new Error("Invalid email address");
      if (this.data.orgUsers.some((u) => u.id !== userId && normalizeEmail(u.email) === email)) {
        throw new Error("A user with this email already exists");
      }
      user.email = email;
    }
    if (input.rolePreset !== undefined) {
      this.assertPresetForOrg(org, input.rolePreset);
      user.rolePreset = input.rolePreset;
      if (input.permissions === undefined) {
        user.permissions = presetPermissions(org.level, input.rolePreset);
      }
    }
    if (input.permissions !== undefined) {
      user.permissions = sanitizePermissionsForLevel(org.level, input.permissions);
    }
    if (input.status !== undefined) {
      user.status = input.status;
    }

    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: org.level,
      action: "Updated org user",
      target: `${org.name}: ${user.email}`,
    });
    this.persist();
    return { ...user, permissions: [...user.permissions] };
  }

  async suspendOrgUser(orgId: string, userId: string) {
    return this.updateOrgUser(orgId, userId, { status: "Suspended" });
  }

  private assertOrgExists(orgId: string): Org {
    const org = this.data.orgs.find((o) => o.id === orgId);
    if (!org) throw new Error("Organization not found");
    return org;
  }

  private assertPresetForOrg(org: Org, presetKey: string) {
    if (!presetsForLevel(org.level).some((p) => p.key === presetKey)) {
      throw new Error(`Role '${presetKey}' is not valid for a ${org.level} org`);
    }
  }

  async createActivation(input: CreateActivationInput) {
    await delay();
    const partner = this.data.partners.find((p) => p.id === input.partnerId);
    if (!partner) throw new Error("Partner not found");
    if (partner.status !== "Active") throw new Error("Partner is not available for activation");

    const catalogItem = this.data.catalogItems.find(
      (c) => c.id === input.catalogItemId && c.partnerId === input.partnerId && c.status === "Active",
    );
    if (!catalogItem) throw new Error("Catalog item not found");

    const idempotencyKey = buildMerchantActivationIdempotencyKey(input.tenantId, input.catalogItemId);
    const existing = this.data.activations.find(
      (a) => a.idempotencyKey === idempotencyKey && a.status !== "Ended",
    );
    if (existing) {
      return row(this.data, existing);
    }

    const ended = this.data.activations.find(
      (a) =>
        a.tenantId === input.tenantId &&
        a.catalogItemId === input.catalogItemId &&
        a.status === "Ended",
    );
    if (ended) {
      throw new Error("This offering was previously ended and cannot be re-activated.");
    }

    const sell = input.resalePrice ?? merchantListedSellPrice(catalogItem);
    const now = new Date().toISOString();

    const activation: MerchantActivation = {
      id: uid("act"),
      partnerId: input.partnerId,
      tenantId: input.tenantId,
      merchantName: input.merchantName,
      catalogItemId: catalogItem.id,
      catalogItemName: catalogItem.name,
      resalePrice: { ...sell },
      status: "Active",
      activatedAt: now,
      idempotencyKey,
      fees:
        partner.participationMode === "SubscriptionFee"
          ? {
              subscription: { enabled: true, amountInclusive: { ...sell }, payer: "Merchant" },
              perTransaction: { enabled: false, amountInclusive: { amount: 1, currency: "SAR", vatInclusive: true }, payer: "Merchant" },
            }
          : undefined,
    };
    this.data.activations.push(activation);
    const wf = workflowFor(this.data, activation.id);
    wf.stage = "Active";
    appendAudit(this.data, {
      actor: input.merchantName,
      role: "MerchantPreview",
      action: "Instant partner activation (merchant self-service)",
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

  async setActivationFees(activationId: string, fees: ActivationFeeConfig) {
    await delay();
    const activation = this.data.activations.find((a) => a.id === activationId);
    if (!activation) throw new Error("Activation not found");
    // Config + display only — never posts a journal (the fee journal stays OFF).
    activation.fees = fees;
    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: "PlatformAdmin",
      action: "Updated activation fee matrix (display only)",
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
      tenantId: original.tenantId,
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

  private nextReceiptNo(year: number): string {
    const seq = this.data.receipts.length + 1;
    return `ZR-${year}-${String(seq).padStart(4, "0")}`;
  }

  async recordPayment(periodId: string, input: RecordPaymentInput) {
    await delay();
    const period = this.data.billingPeriods.find((p) => p.id === periodId);
    if (!period) throw new Error("Billing period not found");

    const amount = Math.round(input.amount * 100) / 100;
    if (!(amount > 0)) throw new Error("Payment amount must be positive");

    const date = input.date || new Date().toISOString();
    const payment = {
      id: uid("pay"),
      amount: { amount, currency: period.feeInclusive.currency, vatInclusive: true },
      date,
      method: input.method,
      reference: input.reference.trim() || `${input.method}-${uid("ref").toUpperCase()}`,
    };
    period.payments = [...(period.payments ?? []), payment];
    period.paymentStatus = deriveInvoicePayment(period).status;

    const receipt: Receipt = {
      id: uid("rcpt"),
      receiptNo: this.nextReceiptNo(new Date(date).getFullYear()),
      invoiceRef: period.invoiceNumber ?? period.billingChargeId,
      billingPeriodId: period.id,
      partnerId: period.partnerId,
      tenantId: period.tenantId,
      merchantName: period.merchantName,
      amount: { ...payment.amount },
      date,
      method: input.method,
      sent: false,
    };
    this.data.receipts.unshift(receipt);

    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: "Accountant",
      action: "Recorded payment (BETA)",
      target: period.id,
    });
    this.persist();
    return { period: { ...period }, receipt: { ...receipt } };
  }

  async sendReceipt(receiptId: string) {
    await delay();
    const receipt = this.data.receipts.find((r) => r.id === receiptId);
    if (!receipt) throw new Error("Receipt not found");
    receipt.sent = true;
    receipt.sentAt = new Date().toISOString();
    appendAudit(this.data, {
      actor: actorNameSafe(),
      role: "Accountant",
      action: "Sent receipt",
      target: receipt.id,
    });
    this.persist();
    return { ...receipt };
  }

  async resetDemoData() {
    await delay();
    this.data = resetToSeedData();
    resetOrgProfiles();
    recalcKpis(this.data);
    notifyPortalDataChanged();
  }

  async getOrgProfile(scope: OrgProfileScope) {
    await delay();
    return readOrgProfile(scope);
  }

  async saveOrgProfile(scope: OrgProfileScope, profile: OrgProfile) {
    await delay();
    return writeOrgProfile(scope, profile);
  }
}

function actorNameSafe() {
  return "Portal user";
}

function normalizeEmail(email: string) {
  return email.trim().toLowerCase();
}

function slug(value: string) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/(^-|-$)/g, "") || "org";
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
