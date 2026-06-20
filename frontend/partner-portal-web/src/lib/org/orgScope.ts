/**
 * Org data-access scope engine (PURE — no storage, no React).
 *
 * HARD MODEL:
 *   1. Default = STRICT ISOLATION. Every record belongs to one org; an org sees only its own.
 *   2. EXCEPTION = the ACTIVATION bridge (and the flow that inherits it). An activation links a
 *      specific partner (partnerId) and a specific merchant (tenantId). It is SHARED with exactly
 *      that partner org, that merchant org, and the platform — nobody else.
 *
 * Implementation: SHARED records are dual-keyed (partnerId + tenantId). A party sees a shared
 * record iff it matches its own partnerId OR its own tenantId (or it is Platform). STANDALONE
 * partner records (catalog, webhooks, credentials) are single-keyed (partnerId) and are visible
 * ONLY to the owning partner + platform — never to merchants, never to other partners.
 *
 * This mirrors ABP's tenant/partner data filters (TenantId / partnerId predicates) 1:1.
 */
import type {
  MerchantActivation,
  Partner,
  PortalData,
  PortalUser,
} from "@/lib/data/types";
import type { OrgContext } from "./orgModel";

/** Minimal shape of a record that participates in the shared activation bridge. */
export interface SharedFlowRef {
  partnerId: string;
  /** Merchant party. When absent, no merchant can see it (partner/platform only). */
  tenantId?: string;
}

/** Minimal shape of a partner-owned standalone record. */
export interface StandaloneRef {
  partnerId: string;
}

const isPlatform = (ctx: OrgContext) => ctx.level === "Platform";

/**
 * Is the current org a PARTY to this activation? Platform is always a party (top).
 * Partner party = partnerId match. Merchant party = tenantId match.
 */
export function isActivationParty(
  activation: Pick<MerchantActivation, "partnerId" | "tenantId">,
  ctx: OrgContext,
): boolean {
  if (isPlatform(ctx)) return true;
  if (ctx.level === "Partner") return activation.partnerId === ctx.partnerId;
  if (ctx.level === "Merchant") return activation.tenantId === ctx.tenantId;
  return false;
}

/** Shared record (activation / flow): visible to its partner party, its merchant party, or platform. */
export function canSeeSharedFlow(record: SharedFlowRef, ctx: OrgContext): boolean {
  if (isPlatform(ctx)) return true;
  if (ctx.level === "Partner") return record.partnerId === ctx.partnerId;
  if (ctx.level === "Merchant") return !!record.tenantId && record.tenantId === ctx.tenantId;
  return false;
}

/** Standalone partner-owned record: visible ONLY to the owning partner + platform. */
export function canSeeStandalone(record: StandaloneRef, ctx: OrgContext): boolean {
  if (isPlatform(ctx)) return true;
  if (ctx.level === "Partner") return record.partnerId === ctx.partnerId;
  // Merchants and non-party partners never see another org's standalone data.
  return false;
}

/** Partner identities a merchant org legitimately sees = partners it has an activation with. */
function partnerIdsVisibleToMerchant(data: PortalData, ctx: OrgContext): Set<string> {
  return new Set(
    data.activations.filter((a) => a.tenantId === ctx.tenantId).map((a) => a.partnerId),
  );
}

function scopePartners(data: PortalData, ctx: OrgContext): Partner[] {
  if (isPlatform(ctx)) return [...data.partners];
  if (ctx.level === "Partner") return data.partners.filter((p) => p.id === ctx.partnerId);
  // Merchant: only partners it shares an activation with (the bridge identity), nothing else.
  const visible = partnerIdsVisibleToMerchant(data, ctx);
  return data.partners.filter((p) => visible.has(p.id));
}

function scopeUsers(data: PortalData, ctx: OrgContext): PortalUser[] {
  if (isPlatform(ctx)) return [...data.users];
  // Legacy portal users are platform-team users; non-platform orgs manage org users instead.
  if (ctx.level === "Partner") return data.users.filter((u) => u.partnerId === ctx.partnerId);
  return [];
}

/**
 * Produce the data view a given org is allowed to see. Strict isolation everywhere except the
 * shared activation bridge + the flow that inherits it. KPIs are recomputed from the scoped view
 * so each org sees only its own totals.
 */
export function scopePortalData(data: PortalData, ctx: OrgContext): PortalData {
  const activations = data.activations.filter((a) => isActivationParty(a, ctx));
  const activationIds = new Set(activations.map((a) => a.id));

  const settlementCases = data.settlementCases.filter((c) => canSeeSharedFlow(c, ctx));
  const visibleCaseIds = new Set(settlementCases.map((c) => c.id));
  const reversals = data.reversals.filter(
    (r) => canSeeSharedFlow(r, ctx) || visibleCaseIds.has(r.originalCaseId),
  );
  const reflectedOrders = data.reflectedOrders.filter((o) => canSeeSharedFlow(o, ctx));
  const billingPeriods = data.billingPeriods.filter((b) => canSeeSharedFlow(b, ctx));

  // Standalone partner-owned data — never shared with merchants.
  const webhookEndpoints = data.webhookEndpoints.filter((w) => canSeeStandalone(w, ctx));
  const visibleEndpointIds = new Set(webhookEndpoints.map((w) => w.id));
  const webhookDeliveries = data.webhookDeliveries.filter(
    (d) => canSeeStandalone(d, ctx) && visibleEndpointIds.has(d.endpointId),
  );
  const credentials = data.credentials.filter((c) => canSeeStandalone(c, ctx));
  const catalogItems = data.catalogItems.filter((i) => canSeeStandalone(i, ctx));

  const partners = scopePartners(data, ctx);

  const orgs = isPlatform(ctx) ? [...data.orgs] : data.orgs.filter((o) => o.id === ctx.orgId);
  const orgUsers = isPlatform(ctx)
    ? [...data.orgUsers]
    : data.orgUsers.filter((u) => u.orgId === ctx.orgId);

  return {
    partners,
    catalogItems,
    activations,
    activationWorkflows: data.activationWorkflows.filter((w) => activationIds.has(w.activationId)),
    settlementCases,
    reversals,
    reflectedOrders,
    billingPeriods,
    webhookEndpoints,
    webhookDeliveries,
    credentials,
    // Teams + audit are platform-administration data — not shared down.
    teams: isPlatform(ctx) ? [...data.teams] : [],
    auditLog: isPlatform(ctx) ? [...data.auditLog] : [],
    users: scopeUsers(data, ctx),
    orgs,
    orgUsers,
    kpis: {
      totalPartners: partners.filter((p) => p.status === "Active").length,
      activeActivations: activations.filter((a) => a.status === "Active").length,
      settlementTotalSar: settlementCases
        .filter((c) => !c.reversesSettlementCaseId)
        .reduce((s, c) => s + c.journal.totalDebits.amount, 0),
      reflectedOrdersCount: reflectedOrders.length,
      subscriptionFeesMtd: billingPeriods
        .filter((b) => b.status === "Invoiced")
        .reduce((s, b) => s + b.feeInclusive.amount, 0),
    },
  };
}
