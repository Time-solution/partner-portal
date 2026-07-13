/**
 * Gate 2b — service order lifecycle (mock store; FE mirror of the ServiceOrder domain).
 * COMPUTE-ONLY: no posting, no invoice, no payment, no escrow, no settlement wiring. Orders
 * snapshot mode + prices at creation (immutable pair); status history is APPEND-ONLY; the 7-day
 * auto-accept is time-parameterized. Milestones are a DECLARED plan only. Scoped projections:
 * merchant sees SELL/fee only, partner sees BUY only — margin exists on neither (6b standard).
 */
import type { ListingRequirementRow } from "@/lib/catalog/listingSchema";

export type ServiceOrderStatus =
  | "Draft"
  | "RequirementsSubmitted"
  | "InProgress"
  | "Delivered"
  | "Accepted"
  | "Closed"
  | "MerchantCancelled"
  | "PartnerDeclined";

export type ServiceOrderAction =
  | "Created"
  | "RequirementsSubmitted"
  | "PartnerAccepted"
  | "PartnerDeclined"
  | "MerchantCancelled"
  | "Delivered"
  | "MerchantAccepted"
  | "AutoAccepted"
  | "RevisionRequested"
  | "Closed";

export const AUTO_ACCEPT_DAYS = 7;
export const SYSTEM_ACTOR = "System";
export const MILESTONE_MIN_PRICE = 1000;
export const MILESTONE_MIN_ROWS = 2;
export const MILESTONE_MAX_ROWS = 5;
export const MILESTONE_FIRST_MIN_SHARE = 0.4;

export const ANSWER_CAPS = {
  ShortText: 150,
  LongText: 2000,
  Link: 500,
  FileUpload: 200, // declared file NAME only — NO storage/upload in this gate
  MultiChoice: 80,
} as const;

export interface ServiceOrderAnswer {
  orderIndex: number;
  requirementTitle: string; // snapshotted at answer time
  requirementType: ListingRequirementRow["type"];
  answerText: string;
}

export interface ServiceOrderMilestone {
  id: string;
  orderIndex: number;
  title: string;
  amount: number;
}

export interface ServiceOrderHistoryRow {
  orderIndex: number;
  action: ServiceOrderAction;
  fromStatus?: ServiceOrderStatus;
  toStatus: ServiceOrderStatus;
  actor: string;
  note?: string;
  at: string;
}

export interface ServiceOrder {
  id: string;
  tenantId: string;
  partnerId: string;
  catalogItemId: string;
  offeringName: string;
  participationMode: "Principal" | "SubscriptionFee";
  status: ServiceOrderStatus;
  // Immutable snapshot pair (Principal → buy+sell; SubscriptionFee → fee).
  buySnapshot?: number;
  sellSnapshot?: number;
  feeSnapshot?: number;
  currency: string;
  revisionCount: number;
  createdAt: string;
  deliveredAt?: string;
  answers: ServiceOrderAnswer[];
  milestones: ServiceOrderMilestone[];
  history: ServiceOrderHistoryRow[];
}

export type ServiceOrderGuardError =
  | "orderErrIllegalTransition"
  | "orderErrTerminal"
  | "orderErrNoteRequired"
  | "orderErrAnswerMissing"
  | "orderErrAnswerTooLong"
  | "orderErrInvalidChoice"
  | "orderErrMilestoneMinPrice"
  | "orderErrMilestoneRowCount"
  | "orderErrMilestoneFirstShare"
  | "orderErrMilestoneSum"
  | "orderErrMilestoneRow";

const cents = (value: number): number => Math.round(value * 100);
const round2 = (value: number): number => cents(value) / 100;

export const merchantPrice = (order: Pick<ServiceOrder, "sellSnapshot" | "feeSnapshot">): number =>
  order.sellSnapshot ?? order.feeSnapshot ?? 0;

const TERMINAL: ServiceOrderStatus[] = ["Closed", "MerchantCancelled", "PartnerDeclined"];
export const isTerminal = (status: ServiceOrderStatus): boolean => TERMINAL.includes(status);

function append(
  order: ServiceOrder,
  action: ServiceOrderAction,
  to: ServiceOrderStatus,
  actor: string,
  note: string | undefined,
  at: string,
): void {
  order.history.push({
    orderIndex: order.history.length,
    action,
    fromStatus: order.status,
    toStatus: to,
    actor,
    note,
    at,
  });
  order.status = to;
}

// ---- milestone plan validation (mirrors the domain; cents arithmetic — no float drift) ------------

export function validateMilestonePlan(
  price: number,
  rows: Array<Pick<ServiceOrderMilestone, "title" | "amount">>,
): ServiceOrderGuardError | null {
  if (rows.length === 0) return null; // optional
  if (cents(price) < cents(MILESTONE_MIN_PRICE)) return "orderErrMilestoneMinPrice";
  if (rows.length < MILESTONE_MIN_ROWS || rows.length > MILESTONE_MAX_ROWS) return "orderErrMilestoneRowCount";
  for (const row of rows) {
    if (!row.title.trim() || row.title.trim().length > 150 || row.amount <= 0) return "orderErrMilestoneRow";
  }
  const firstMinimum = round2(price * MILESTONE_FIRST_MIN_SHARE);
  if (cents(rows[0].amount) < cents(firstMinimum)) return "orderErrMilestoneFirstShare";
  const sum = rows.reduce((total, row) => total + cents(row.amount), 0);
  if (sum !== cents(price)) return "orderErrMilestoneSum";
  return null;
}

// ---- requirements answers validation (mirrors the domain) ------------------------------------------

export function validateAnswers(
  requirements: ListingRequirementRow[],
  answersByOrderIndex: Record<number, string>,
): ServiceOrderGuardError | null {
  for (const requirement of requirements) {
    const raw = answersByOrderIndex[requirement.orderIndex];
    if (!raw || !raw.trim()) return "orderErrAnswerMissing";
    const answer = raw.trim();
    if (requirement.type === "MultiChoice") {
      if (!requirement.choices.includes(answer)) return "orderErrInvalidChoice";
    } else if (answer.length > ANSWER_CAPS[requirement.type]) {
      return "orderErrAnswerTooLong";
    }
  }
  return null;
}

// ---- lifecycle guards (all mirror the domain 1:1) ---------------------------------------------------

function guardTerminal(order: ServiceOrder): ServiceOrderGuardError | null {
  return isTerminal(order.status) ? "orderErrTerminal" : null;
}

export function submitRequirements(
  order: ServiceOrder,
  requirements: ListingRequirementRow[],
  answersByOrderIndex: Record<number, string>,
  actor: string,
  at: string,
): ServiceOrderGuardError | null {
  const terminal = guardTerminal(order);
  if (terminal) return terminal;
  if (order.status !== "Draft") return "orderErrIllegalTransition";
  const invalid = validateAnswers(requirements, answersByOrderIndex);
  if (invalid) return invalid;

  order.answers = [...requirements]
    .sort((a, b) => a.orderIndex - b.orderIndex)
    .map((requirement, index) => ({
      orderIndex: index,
      requirementTitle: requirement.title, // snapshot — later listing edits never touch this
      requirementType: requirement.type,
      answerText: answersByOrderIndex[requirement.orderIndex].trim(),
    }));
  append(order, "RequirementsSubmitted", "RequirementsSubmitted", actor, undefined, at);
  return null;
}

export function partnerAccept(order: ServiceOrder, actor: string, at: string): ServiceOrderGuardError | null {
  const terminal = guardTerminal(order);
  if (terminal) return terminal;
  if (order.status !== "RequirementsSubmitted") return "orderErrIllegalTransition";
  append(order, "PartnerAccepted", "InProgress", actor, undefined, at);
  return null;
}

export function partnerDecline(order: ServiceOrder, actor: string, note: string, at: string): ServiceOrderGuardError | null {
  if (!note.trim()) return "orderErrNoteRequired";
  const terminal = guardTerminal(order);
  if (terminal) return terminal;
  if (order.status !== "Draft" && order.status !== "RequirementsSubmitted") return "orderErrIllegalTransition";
  append(order, "PartnerDeclined", "PartnerDeclined", actor, note.trim(), at);
  return null;
}

export function merchantCancel(order: ServiceOrder, actor: string, at: string): ServiceOrderGuardError | null {
  const terminal = guardTerminal(order);
  if (terminal) return terminal;
  if (order.status !== "Draft" && order.status !== "RequirementsSubmitted") return "orderErrIllegalTransition";
  append(order, "MerchantCancelled", "MerchantCancelled", actor, undefined, at);
  return null;
}

export function markDelivered(order: ServiceOrder, actor: string, at: string): ServiceOrderGuardError | null {
  const terminal = guardTerminal(order);
  if (terminal) return terminal;
  if (order.status !== "InProgress") return "orderErrIllegalTransition";
  order.deliveredAt = at;
  append(order, "Delivered", "Delivered", actor, undefined, at);
  return null;
}

export function merchantAccept(order: ServiceOrder, actor: string, at: string): ServiceOrderGuardError | null {
  const terminal = guardTerminal(order);
  if (terminal) return terminal;
  if (order.status !== "Delivered") return "orderErrIllegalTransition";
  append(order, "MerchantAccepted", "Accepted", actor, undefined, at);
  // Accepted → Closed is immediate in v1 — kept a distinct transition for the escrow release point.
  append(order, "Closed", "Closed", actor, undefined, at);
  return null;
}

export function requestRevision(order: ServiceOrder, actor: string, note: string, at: string): ServiceOrderGuardError | null {
  if (!note.trim()) return "orderErrNoteRequired";
  const terminal = guardTerminal(order);
  if (terminal) return terminal;
  if (order.status !== "Delivered") return "orderErrIllegalTransition";
  order.revisionCount += 1;
  order.deliveredAt = undefined;
  append(order, "RevisionRequested", "InProgress", actor, note.trim(), at);
  return null;
}

/** Time-parameterized: due when now ≥ deliveredAt + 7 days; System actor; idempotent. */
export function autoAcceptIfDue(order: ServiceOrder, nowIso: string): boolean {
  if (order.status !== "Delivered" || !order.deliveredAt) return false;
  const due = new Date(order.deliveredAt).getTime() + AUTO_ACCEPT_DAYS * 24 * 60 * 60 * 1000;
  if (new Date(nowIso).getTime() < due) return false;
  append(order, "AutoAccepted", "Accepted", SYSTEM_ACTOR, undefined, nowIso);
  append(order, "Closed", "Closed", SYSTEM_ACTOR, undefined, nowIso);
  return true;
}

// ---- scoped projections (fresh literals — the 6b structural standard) --------------------------------

export interface MerchantOrderView {
  id: string;
  offeringName: string;
  status: ServiceOrderStatus;
  priceAmount: number; // SELL/fee only
  currency: string;
  revisionCount: number;
  deliveredAt?: string;
  answers: ServiceOrderAnswer[];
  milestones: Array<{ orderIndex: number; title: string; amount: number }>;
  history: ServiceOrderHistoryRow[];
}

export interface PartnerOrderView {
  id: string;
  offeringName: string;
  status: ServiceOrderStatus;
  buyAmount?: number; // BUY only (the partner receivable)
  currency: string;
  revisionCount: number;
  deliveredAt?: string;
  answers: ServiceOrderAnswer[];
  history: ServiceOrderHistoryRow[];
}

export function merchantOrderView(order: ServiceOrder): MerchantOrderView {
  return {
    id: order.id,
    offeringName: order.offeringName,
    status: order.status,
    priceAmount: merchantPrice(order),
    currency: order.currency,
    revisionCount: order.revisionCount,
    deliveredAt: order.deliveredAt,
    answers: order.answers.map((a) => ({ ...a })),
    milestones: order.milestones.map((m) => ({ orderIndex: m.orderIndex, title: m.title, amount: m.amount })),
    history: order.history.map((h) => ({ ...h })),
  };
}

export function partnerOrderView(order: ServiceOrder): PartnerOrderView {
  return {
    id: order.id,
    offeringName: order.offeringName,
    status: order.status,
    buyAmount: order.buySnapshot,
    currency: order.currency,
    revisionCount: order.revisionCount,
    deliveredAt: order.deliveredAt,
    answers: order.answers.map((a) => ({ ...a })),
    history: order.history.map((h) => ({ ...h })),
  };
}

// ---- store (localStorage sibling — the established mock pattern) + demo seed --------------------------

const STORAGE_KEY = "zahy-service-orders-v1";
const DEMO_TENANT = "33333333-3333-3333-3333-333333333001";
const DEMO_PARTNER = "22222222-2222-2222-2222-222222222004";
const DEMO_ITEM = "a1000003-0003-4000-8000-000000000003";

let orderSeq = 0;
const newId = (): string => `so-${Date.now()}-${++orderSeq}`;

export function createOrder(input: {
  catalogItemId: string;
  offeringName: string;
  partnerId: string;
  tenantId: string;
  participationMode: "Principal" | "SubscriptionFee";
  buy?: number;
  sellOrFee: number;
  milestones?: Array<{ title: string; amount: number }>;
  actor: string;
  at: string;
}): { order?: ServiceOrder; error?: ServiceOrderGuardError } {
  const plan = input.milestones ?? [];
  const planError = validateMilestonePlan(input.sellOrFee, plan);
  if (planError) return { error: planError };

  const order: ServiceOrder = {
    id: newId(),
    tenantId: input.tenantId,
    partnerId: input.partnerId,
    catalogItemId: input.catalogItemId,
    offeringName: input.offeringName,
    participationMode: input.participationMode,
    status: "Draft",
    buySnapshot: input.participationMode === "Principal" ? round2(input.buy ?? 0) : undefined,
    sellSnapshot: input.participationMode === "Principal" ? round2(input.sellOrFee) : undefined,
    feeSnapshot: input.participationMode === "SubscriptionFee" ? round2(input.sellOrFee) : undefined,
    currency: "SAR",
    revisionCount: 0,
    createdAt: input.at,
    answers: [],
    milestones: plan.map((m, i) => ({ id: `${newId()}-m${i}`, orderIndex: i, title: m.title.trim(), amount: round2(m.amount) })),
    history: [],
  };
  order.history.push({
    orderIndex: 0,
    action: "Created",
    toStatus: "Draft",
    actor: input.actor,
    at: input.at,
  });
  return { order };
}

function seedDefaults(): ServiceOrder[] {
  const daysAgo = (n: number): string => new Date(Date.now() - n * 24 * 60 * 60 * 1000).toISOString();
  const base = {
    catalogItemId: DEMO_ITEM,
    offeringName: "Basic tier — WhatsApp",
    partnerId: DEMO_PARTNER,
    tenantId: DEMO_TENANT,
    participationMode: "Principal" as const,
    buy: 49,
    actor: "merchant-demo",
  };
  const requirements: ListingRequirementRow[] = [
    { id: "r1", orderIndex: 0, title: "نبذة عن نشاط المتجر", type: "ShortText", choices: [] },
    { id: "r2", orderIndex: 1, title: "شعار المتجر بجودة عالية", type: "FileUpload", choices: [] },
    { id: "r3", orderIndex: 2, title: "الفئة المستهدفة", type: "MultiChoice", choices: ["أفراد", "شركات", "الاثنان معًا"] },
  ];
  const answers = { 0: "متجر عطور نسائية في الرياض", 1: "logo-final-v3.png", 2: "الاثنان معًا" };

  // 1) InProgress
  const inProgress = createOrder({ ...base, sellOrFee: 75, at: daysAgo(4) }).order!;
  submitRequirements(inProgress, requirements, answers, "merchant-demo", daysAgo(4));
  partnerAccept(inProgress, "partner-demo", daysAgo(3));

  // 2) Delivered ~6 days ago — auto-accept nearly due (demoable)
  const delivered = createOrder({ ...base, sellOrFee: 75, at: daysAgo(9) }).order!;
  submitRequirements(delivered, requirements, answers, "merchant-demo", daysAgo(9));
  partnerAccept(delivered, "partner-demo", daysAgo(8));
  markDelivered(delivered, "partner-demo", daysAgo(6));

  // 3) Closed
  const closed = createOrder({ ...base, sellOrFee: 75, at: daysAgo(20) }).order!;
  submitRequirements(closed, requirements, answers, "merchant-demo", daysAgo(20));
  partnerAccept(closed, "partner-demo", daysAgo(19));
  markDelivered(closed, "partner-demo", daysAgo(15));
  merchantAccept(closed, "merchant-demo", daysAgo(14));

  // 4) ≥1000 SAR with a valid 40/30/30 plan
  const withPlan = createOrder({
    ...base,
    sellOrFee: 1500,
    milestones: [
      { title: "دفعة أولى — بدء التنفيذ", amount: 600 },
      { title: "دفعة ثانية — تسليم أولي", amount: 450 },
      { title: "دفعة أخيرة — الإقفال", amount: 450 },
    ],
    at: daysAgo(1),
  }).order!;
  submitRequirements(withPlan, requirements, answers, "merchant-demo", daysAgo(1));

  return [inProgress, delivered, closed, withPlan];
}

function load(): ServiceOrder[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as ServiceOrder[];
      if (Array.isArray(parsed) && parsed.length > 0) return parsed;
    }
    // Seed once and PERSIST so ids stay stable across loads.
    const seeded = seedDefaults();
    persist(seeded);
    return seeded;
  } catch {
    return seedDefaults();
  }
}

function persist(list: ServiceOrder[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
  } catch {
    // mock store
  }
}

export function listOrdersForTenant(tenantId: string): ServiceOrder[] {
  return load().filter((o) => o.tenantId === tenantId);
}

export function listOrdersForPartner(partnerId: string): ServiceOrder[] {
  return load().filter((o) => o.partnerId === partnerId);
}

export function saveOrder(order: ServiceOrder): void {
  const list = load();
  const index = list.findIndex((o) => o.id === order.id);
  if (index >= 0) list[index] = order;
  else list.push(order);
  persist(list);
}

export function mutateOrder(
  orderId: string,
  mutate: (order: ServiceOrder) => ServiceOrderGuardError | null,
): ServiceOrderGuardError | null {
  const list = load();
  const order = list.find((o) => o.id === orderId);
  if (!order) return null;
  const error = mutate(order);
  if (!error) persist(list);
  return error;
}

/** Admin sweep — v1 auto-accept trigger. Returns how many orders auto-accepted (idempotent). */
export function runAutoAcceptSweep(nowIso: string): number {
  const list = load();
  let count = 0;
  for (const order of list) {
    if (autoAcceptIfDue(order, nowIso)) count++;
  }
  if (count > 0) persist(list);
  return count;
}

export function resetServiceOrders(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // mock store
  }
}
