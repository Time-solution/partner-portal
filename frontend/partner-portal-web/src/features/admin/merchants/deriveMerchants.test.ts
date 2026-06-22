import { describe, expect, it } from "vitest";
import type { MerchantActivation, Partner, PortalData } from "@/lib/data/types";
import type { Org } from "@/lib/org/orgModel";
import { deriveMerchants, findMerchant } from "./deriveMerchants";

const money = { amount: 0, currency: "SAR", vatInclusive: false };

function partner(id: string, name: string, mode: Partner["participationMode"]): Partner {
  return {
    id,
    legalName: name,
    tradeName: name,
    type: "Aggregator",
    status: "Active",
    primaryContactEmail: `${id}@p.sa`,
    participationMode: mode,
    accentClass: "",
  };
}

function activation(
  id: string,
  partnerId: string,
  tenantId: string,
  merchantName: string,
  status: MerchantActivation["status"],
): MerchantActivation {
  return {
    id,
    partnerId,
    tenantId,
    merchantName,
    catalogItemId: `cat-${partnerId}`,
    catalogItemName: "Delivery",
    resalePrice: money,
    status,
    activatedAt: "2026-05-01T00:00:00Z",
  };
}

function merchantOrg(tenantId: string, name: string, status: Org["status"]): Org {
  return {
    id: `org-${tenantId}`,
    level: "Merchant",
    name,
    tenantId,
    status,
    createdAt: "2026-01-01T00:00:00Z",
  };
}

function baseData(over: Partial<PortalData>): PortalData {
  return {
    partners: [],
    catalogItems: [],
    activations: [],
    activationWorkflows: [],
    settlementCases: [],
    reversals: [],
    reflectedOrders: [],
    billingPeriods: [],
    receipts: [],
    commissionLedger: [],
    webhookEndpoints: [],
    webhookDeliveries: [],
    credentials: [],
    teams: [],
    auditLog: [],
    users: [],
    orgs: [],
    orgUsers: [],
    manualInvoices: [],
    kpis: {
      totalPartners: 0,
      activeActivations: 0,
      settlementTotalSar: 0,
      reflectedOrdersCount: 0,
      subscriptionFeesMtd: 0,
    },
    ...over,
  };
}

describe("deriveMerchants", () => {
  const data = baseData({
    partners: [
      partner("p-salasa", "Salasa", "ReflectionOnly"),
      partner("p-jahez", "Jahez", "Principal"),
    ],
    activations: [
      activation("a1", "p-salasa", "t-quickbites", "Quick Bites", "Active"),
      activation("a2", "p-jahez", "t-quickbites", "Quick Bites", "Pending"),
      activation("a3", "p-jahez", "t-bakery", "Bakery", "Ended"),
    ],
    orgs: [merchantOrg("t-quickbites", "Quick Bites Co.", "Active")],
  });

  it("groups activations into one merchant per tenant", () => {
    const merchants = deriveMerchants(data);
    expect(merchants.map((m) => m.tenantId).sort()).toEqual(["t-bakery", "t-quickbites"]);
  });

  it("prefers the merchant org name and status when present", () => {
    const m = findMerchant(data, "t-quickbites")!;
    expect(m.name).toBe("Quick Bites Co.");
    expect(m.status).toBe("Active");
    expect(m.orgId).toBe("org-t-quickbites");
  });

  it("falls back to activation merchantName when no org exists", () => {
    const m = findMerchant(data, "t-bakery")!;
    expect(m.name).toBe("Bakery");
    expect(m.orgId).toBeUndefined();
  });

  it("counts only active activations", () => {
    const m = findMerchant(data, "t-quickbites")!;
    expect(m.activeActivationCount).toBe(1);
    expect(m.totalActivationCount).toBe(2);
  });

  it("derives connected partners with participation mode from the partner", () => {
    const m = findMerchant(data, "t-quickbites")!;
    expect(m.connectedPartners.map((p) => `${p.partnerName}:${p.participationMode}`)).toEqual([
      "Jahez:Principal",
      "Salasa:ReflectionOnly",
    ]);
  });
});
