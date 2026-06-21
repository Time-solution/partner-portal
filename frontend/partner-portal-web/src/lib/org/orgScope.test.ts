import { describe, expect, it } from "vitest";
import type {
  MerchantActivation,
  PartnerCatalogItem,
  PortalData,
  ReflectedPartnerOrder,
  SettlementCase,
  SettlementJournal,
  SubscriptionBillingPeriod,
  WebhookEndpoint,
} from "@/lib/data/types";
import type { OrgContext } from "./orgModel";
import { canSeeSharedFlow, canSeeStandalone, isActivationParty, scopePortalData } from "./orgScope";

const PA = "partner-A";
const PB = "partner-B";
const TM = "tenant-M";
const TN = "tenant-N";

const journal: SettlementJournal = {
  currency: "SAR",
  totalDebits: { amount: 13, currency: "SAR", vatInclusive: false },
  totalCredits: { amount: 13, currency: "SAR", vatInclusive: false },
  lines: [],
};

const sar = (amount: number) => ({ amount, currency: "SAR", vatInclusive: true });

const activation = (id: string, partnerId: string, tenantId: string): MerchantActivation => ({
  id,
  partnerId,
  tenantId,
  merchantName: `Merchant ${tenantId}`,
  catalogItemId: `cat-${partnerId}`,
  catalogItemName: "Item",
  resalePrice: sar(13),
  status: "Active",
});

const settlement = (id: string, partnerId: string, tenantId: string): SettlementCase => ({
  id,
  partnerId,
  partnerName: partnerId,
  tenantId,
  externalTransactionId: `order:${id}:v1`,
  book: "Marketplace",
  state: "Allocated",
  journal,
  createdAt: "2026-06-01T00:00:00Z",
});

const reflected = (id: string, partnerId: string, tenantId: string): ReflectedPartnerOrder => ({
  id,
  partnerId,
  partnerName: partnerId,
  tenantId,
  merchantName: `Merchant ${tenantId}`,
  externalTransactionId: `order:${id}:v1`,
  orderLineId: `${id}-l1`,
  posSyncStatus: "Reflected",
  reflectedAt: "2026-06-01T00:00:00Z",
});

const billing = (id: string, partnerId: string, tenantId: string): SubscriptionBillingPeriod => ({
  id,
  partnerId,
  tenantId,
  merchantName: `Merchant ${tenantId}`,
  periodKey: "2026-06",
  feeInclusive: sar(115),
  outputVat: 15,
  netFee: 100,
  billingChargeId: `chg-${id}`,
  journalBalanced: true,
  status: "Invoiced",
});

const catalogItem = (partnerId: string): PartnerCatalogItem => ({
  id: `cat-${partnerId}`,
  partnerId,
  code: "CODE",
  name: "Item",
  offeringKind: "ServiceOneOff",
  participationMode: "Principal",
  partnerCost: sar(10),
  settlementBook: "Marketplace",
  status: "Active",
});

const webhook = (partnerId: string): WebhookEndpoint => ({
  id: `wh-${partnerId}`,
  partnerId,
  partnerName: partnerId,
  url: "https://x",
  eventTypes: [],
  status: "Active",
  secretHint: "•",
  createdAt: "2026-06-01T00:00:00Z",
});

function makeData(): PortalData {
  return {
    partners: [
      { id: PA, legalName: "A", type: "Service", status: "Active", primaryContactEmail: "a@a", participationMode: "Principal", accentClass: "" },
      { id: PB, legalName: "B", type: "Service", status: "Active", primaryContactEmail: "b@b", participationMode: "Principal", accentClass: "" },
    ],
    catalogItems: [catalogItem(PA), catalogItem(PB)],
    activations: [activation("act-AM", PA, TM), activation("act-BN", PB, TN)],
    activationWorkflows: [
      { activationId: "act-AM", stage: "Active" },
      { activationId: "act-BN", stage: "Active" },
    ],
    settlementCases: [settlement("cs-AM", PA, TM), settlement("cs-BN", PB, TN)],
    reversals: [],
    reflectedOrders: [reflected("ro-AM", PA, TM), reflected("ro-BN", PB, TN)],
    billingPeriods: [billing("b-AM", PA, TM), billing("b-BN", PB, TN)],
    receipts: [],
    webhookEndpoints: [webhook(PA), webhook(PB)],
    webhookDeliveries: [],
    credentials: [],
    kpis: { totalPartners: 0, activeActivations: 0, settlementTotalSar: 0, reflectedOrdersCount: 0, subscriptionFeesMtd: 0 },
    teams: [{ id: "t1", name: "Core", kind: "Core", memberCount: 1 }],
    auditLog: [{ id: "a1", at: "2026-06-01T00:00:00Z", actor: "x", role: "PlatformAdmin", action: "x", target: "x" }],
    users: [],
    orgs: [],
    orgUsers: [],
  };
}

const platform: OrgContext = { orgId: "org-platform", level: "Platform" };
const partnerA: OrgContext = { orgId: "org-A", level: "Partner", partnerId: PA };
const partnerB: OrgContext = { orgId: "org-B", level: "Partner", partnerId: PB };
const merchantM: OrgContext = { orgId: "org-M", level: "Merchant", tenantId: TM };
const merchantN: OrgContext = { orgId: "org-N", level: "Merchant", tenantId: TN };

const ids = <T extends { id: string }>(rows: T[]) => rows.map((r) => r.id).sort();

describe("isActivationParty", () => {
  const act = activation("act-AM", PA, TM);
  it("platform is always a party", () => expect(isActivationParty(act, platform)).toBe(true));
  it("matching partner is a party", () => expect(isActivationParty(act, partnerA)).toBe(true));
  it("matching merchant is a party", () => expect(isActivationParty(act, merchantM)).toBe(true));
  it("other partner is NOT a party", () => expect(isActivationParty(act, partnerB)).toBe(false));
  it("other merchant is NOT a party", () => expect(isActivationParty(act, merchantN)).toBe(false));
});

describe("activation sharing (the bridge)", () => {
  const data = makeData();

  it("Partner A sees its activation with Merchant M", () => {
    expect(ids(scopePortalData(data, partnerA).activations)).toEqual(["act-AM"]);
  });

  it("Partner B (not a party) does NOT see A↔M activation", () => {
    expect(ids(scopePortalData(data, partnerB).activations)).toEqual(["act-BN"]);
  });

  it("Merchant M sees its activation with Partner A", () => {
    expect(ids(scopePortalData(data, merchantM).activations)).toEqual(["act-AM"]);
  });

  it("Merchant N (not a party) does NOT see A↔M activation", () => {
    expect(ids(scopePortalData(data, merchantN).activations)).toEqual(["act-BN"]);
  });

  it("Platform sees ALL activations", () => {
    expect(ids(scopePortalData(data, platform).activations)).toEqual(["act-AM", "act-BN"]);
  });
});

describe("flow inherits activation sharing (orders / settlement / billing)", () => {
  const data = makeData();

  it("settlement of A↔M is visible to BOTH parties, blocked for others", () => {
    expect(ids(scopePortalData(data, partnerA).settlementCases)).toEqual(["cs-AM"]);
    expect(ids(scopePortalData(data, merchantM).settlementCases)).toEqual(["cs-AM"]);
    expect(ids(scopePortalData(data, partnerB).settlementCases)).toEqual(["cs-BN"]);
    expect(ids(scopePortalData(data, merchantN).settlementCases)).toEqual(["cs-BN"]);
  });

  it("reflected orders of A↔M follow the same sharing", () => {
    expect(ids(scopePortalData(data, partnerA).reflectedOrders)).toEqual(["ro-AM"]);
    expect(ids(scopePortalData(data, merchantM).reflectedOrders)).toEqual(["ro-AM"]);
    expect(scopePortalData(data, merchantN).reflectedOrders.some((o) => o.id === "ro-AM")).toBe(false);
  });

  it("billing of A↔M follows the same sharing", () => {
    expect(ids(scopePortalData(data, partnerA).billingPeriods)).toEqual(["b-AM"]);
    expect(ids(scopePortalData(data, merchantM).billingPeriods)).toEqual(["b-AM"]);
    expect(scopePortalData(data, partnerB).billingPeriods.some((b) => b.id === "b-AM")).toBe(false);
  });

  it("canSeeSharedFlow direct predicate", () => {
    const cs = settlement("cs-AM", PA, TM);
    expect(canSeeSharedFlow(cs, partnerA)).toBe(true);
    expect(canSeeSharedFlow(cs, merchantM)).toBe(true);
    expect(canSeeSharedFlow(cs, partnerB)).toBe(false);
    expect(canSeeSharedFlow(cs, merchantN)).toBe(false);
  });
});

describe("strict isolation of standalone (non-activation) data", () => {
  const data = makeData();

  it("partner standalone catalog/webhooks visible only to owning partner + platform", () => {
    expect(ids(scopePortalData(data, partnerA).catalogItems)).toEqual(["cat-partner-A"]);
    expect(ids(scopePortalData(data, partnerA).webhookEndpoints)).toEqual(["wh-partner-A"]);
    // Other partner cannot see A's standalone data.
    expect(scopePortalData(data, partnerB).catalogItems.some((i) => i.id === "cat-partner-A")).toBe(false);
  });

  it("merchants NEVER see partner standalone data (catalog / webhooks / credentials)", () => {
    const view = scopePortalData(data, merchantM);
    expect(view.catalogItems).toHaveLength(0);
    expect(view.webhookEndpoints).toHaveLength(0);
    expect(view.credentials).toHaveLength(0);
  });

  it("standalone predicate blocks merchants and non-owning partners", () => {
    expect(canSeeStandalone(catalogItem(PA), partnerA)).toBe(true);
    expect(canSeeStandalone(catalogItem(PA), partnerB)).toBe(false);
    expect(canSeeStandalone(catalogItem(PA), merchantM)).toBe(false);
    expect(canSeeStandalone(catalogItem(PA), platform)).toBe(true);
  });

  it("teams + audit are platform-only", () => {
    expect(scopePortalData(data, platform).teams).toHaveLength(1);
    expect(scopePortalData(data, partnerA).teams).toHaveLength(0);
    expect(scopePortalData(data, merchantM).auditLog).toHaveLength(0);
  });
});

describe("scoped KPIs reflect only the org's visible data", () => {
  const data = makeData();
  it("each party counts only its own activation", () => {
    expect(scopePortalData(data, partnerA).kpis.activeActivations).toBe(1);
    expect(scopePortalData(data, merchantM).kpis.activeActivations).toBe(1);
    expect(scopePortalData(data, platform).kpis.activeActivations).toBe(2);
  });
});

describe("merchant sees only partners it is activated with", () => {
  const data = makeData();
  it("Merchant M sees Partner A only", () => {
    expect(ids(scopePortalData(data, merchantM).partners)).toEqual([PA]);
  });
  it("Merchant N sees Partner B only", () => {
    expect(ids(scopePortalData(data, merchantN).partners)).toEqual([PB]);
  });
});
