import { getPortalDataSource } from "@/lib/data";
import type { PortalData } from "@/lib/data/types";
import { deriveSettlementSummary } from "@/lib/data/types";
import {
  moduleLabelKey,
  resolvePartnerModule,
  type PartnerBusinessModuleId,
} from "@/lib/rbac/partnerModules";

const CHART_MODULES: PartnerBusinessModuleId[] = [
  "delivery-service",
  "commerce",
  "fnb",
  "subscriptions",
  "marketplace",
];

export interface ActivityKpis {
  activePartners: number;
  activeActivations: number;
  reflectedOrders: number;
  reversals: number;
}

export interface ModuleTransactionPoint {
  moduleId: PartnerBusinessModuleId;
  labelKey: string;
  volume: number;
  amountSar: number;
}

export interface PendingDoneRow {
  categoryKey: string;
  pending: number;
  done: number;
  /** Webhook-only third segment */
  retrying?: number;
  dlq?: number;
}

export interface SettlementTrendPoint {
  periodKey: string;
  settlementSar: number;
  marginSar: number;
  vatSar: number;
}

export interface BillingRevenuePoint {
  periodKey: string;
  revenueSar: number;
  vatSar: number;
}

export interface ReversalImpactPoint {
  id: string;
  partnerName: string;
  amountSar: number;
}

export interface PortalChartAnalytics {
  kpis: ActivityKpis;
  settlementCaseCount: number;
  transactionsByModule: ModuleTransactionPoint[];
  pendingVsDone: PendingDoneRow[];
  settlementTrend: SettlementTrendPoint[];
  billingRevenue: BillingRevenuePoint[];
  pendingApprovals: number;
  reversalImpact: ReversalImpactPoint[];
}

function partnerModuleMap(data: PortalData): Map<string, PartnerBusinessModuleId> {
  return new Map(data.partners.map((p) => [p.id, resolvePartnerModule(p)]));
}

function emptyModuleTotals(): Record<PartnerBusinessModuleId, { volume: number; amountSar: number }> {
  return {
    "delivery-service": { volume: 0, amountSar: 0 },
    commerce: { volume: 0, amountSar: 0 },
    fnb: { volume: 0, amountSar: 0 },
    subscriptions: { volume: 0, amountSar: 0 },
    marketplace: { volume: 0, amountSar: 0 },
    consignment: { volume: 0, amountSar: 0 },
  };
}

export function buildChartAnalytics(data: PortalData): PortalChartAnalytics {
  const modMap = partnerModuleMap(data);
  const totals = emptyModuleTotals();

  const cases = data.settlementCases.filter((c) => !c.reversesSettlementCaseId);

  for (const c of cases) {
    const mod = modMap.get(c.partnerId);
    if (!mod || mod === "consignment") continue;
    totals[mod].volume += 1;
    totals[mod].amountSar += deriveSettlementSummary(c.journal).sellPrice.amount;
  }

  for (const o of data.reflectedOrders) {
    const mod = modMap.get(o.partnerId);
    if (mod === "commerce" || mod === "fnb") {
      totals[mod].volume += 1;
    }
  }

  for (const b of data.billingPeriods) {
    const mod = modMap.get(b.partnerId);
    if (mod === "subscriptions") {
      totals[mod].volume += 1;
      totals[mod].amountSar += b.feeInclusive.amount;
    }
  }

  for (const a of data.activations) {
    const mod = modMap.get(a.partnerId);
    if (mod === "marketplace") {
      totals[mod].volume += 1;
      if (a.status === "Active") {
        totals[mod].amountSar += a.resalePrice.amount;
      }
    }
  }

  const transactionsByModule = CHART_MODULES.map((moduleId) => ({
    moduleId,
    labelKey: moduleLabelKey(moduleId),
    volume: totals[moduleId].volume,
    amountSar: Math.round(totals[moduleId].amountSar * 100) / 100,
  }));

  const webhookDelivered = data.webhookDeliveries.filter((d) => d.status === "Delivered").length;
  const webhookRetrying = data.webhookDeliveries.filter((d) => d.status === "Retrying").length;
  const webhookDlq = data.webhookDeliveries.filter((d) => d.status === "Dlq").length;

  const pendingVsDone: PendingDoneRow[] = [
    {
      categoryKey: "chartCategory_activations",
      pending: data.activations.filter((a) => a.status !== "Active").length,
      done: data.activations.filter((a) => a.status === "Active").length,
    },
    {
      categoryKey: "chartCategory_settlements",
      pending: cases.filter((c) => c.state === "Collected" || c.state === "Allocated").length,
      done: cases.filter((c) => c.state === "Invoiced" || c.state === "Cleared").length,
    },
    {
      categoryKey: "chartCategory_billing",
      pending: data.billingPeriods.filter((p) => p.status === "Charged").length,
      done: data.billingPeriods.filter((p) => p.status === "Invoiced").length,
    },
    {
      categoryKey: "chartCategory_webhooks",
      pending: webhookRetrying + webhookDlq,
      done: webhookDelivered,
      retrying: webhookRetrying,
      dlq: webhookDlq,
    },
  ];

  const trendMap = new Map<string, SettlementTrendPoint>();
  for (const c of cases) {
    const key = c.createdAt.slice(0, 10);
    const summary = deriveSettlementSummary(c.journal);
    const existing = trendMap.get(key) ?? {
      periodKey: key,
      settlementSar: 0,
      marginSar: 0,
      vatSar: 0,
    };
    existing.settlementSar += summary.sellPrice.amount;
    existing.marginSar += summary.margin;
    existing.vatSar += summary.netVatToZatca;
    trendMap.set(key, existing);
  }
  const settlementTrend = [...trendMap.values()]
    .map((p) => ({
      ...p,
      settlementSar: Math.round(p.settlementSar * 100) / 100,
      marginSar: Math.round(p.marginSar * 100) / 100,
      vatSar: Math.round(p.vatSar * 100) / 100,
    }))
    .sort((a, b) => a.periodKey.localeCompare(b.periodKey));

  const billingRevenue = data.billingPeriods
    .map((p) => ({
      periodKey: p.periodKey,
      revenueSar: p.feeInclusive.amount,
      vatSar: p.outputVat,
    }))
    .sort((a, b) => a.periodKey.localeCompare(b.periodKey));

  const pendingApprovals = data.activationWorkflows.filter(
    (w) => w.stage === "PsmRequested" || w.stage === "AccountantTermsSet",
  ).length;

  const reversalImpact = data.reversals.map((r) => ({
    id: r.id,
    partnerName: r.partnerName,
    amountSar: r.journal.totalDebits.amount,
  }));

  return {
    kpis: {
      activePartners: data.partners.filter((p) => p.status === "Active").length,
      activeActivations: data.activations.filter((a) => a.status === "Active").length,
      reflectedOrders: data.reflectedOrders.length,
      reversals: data.reversals.length,
    },
    settlementCaseCount: cases.length,
    transactionsByModule,
    pendingVsDone,
    settlementTrend,
    billingRevenue,
    pendingApprovals,
    reversalImpact,
  };
}

export async function loadPortalChartAnalytics(): Promise<PortalChartAnalytics> {
  const data = await getPortalDataSource().getAll();
  return buildChartAnalytics(data);
}
