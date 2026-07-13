import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ListingRequirementRow } from "@/lib/catalog/listingSchema";
import {
  autoAcceptIfDue,
  createOrder,
  listOrdersForPartner,
  listOrdersForTenant,
  merchantAccept,
  merchantCancel,
  merchantOrderView,
  mutateOrder,
  partnerAccept,
  partnerDecline,
  partnerOrderView,
  markDelivered,
  requestRevision,
  resetServiceOrders,
  runAutoAcceptSweep,
  submitRequirements,
  SYSTEM_ACTOR,
  validateMilestonePlan,
  type ServiceOrder,
} from "./serviceOrders";

function stubLocalStorage() {
  const bag = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => bag.get(k) ?? null,
    setItem: (k: string, v: string) => bag.set(k, v),
    removeItem: (k: string) => bag.delete(k),
    clear: () => bag.clear(),
    key: () => null,
    length: 0,
  });
}

const T0 = "2026-07-01T10:00:00.000Z";
const at = (offsetDays: number): string =>
  new Date(new Date(T0).getTime() + offsetDays * 24 * 60 * 60 * 1000).toISOString();

const REQS: ListingRequirementRow[] = [
  { id: "r1", orderIndex: 0, title: "نبذة", type: "ShortText", choices: [] },
  { id: "r2", orderIndex: 1, title: "الشعار", type: "FileUpload", choices: [] },
  { id: "r3", orderIndex: 2, title: "الفئة", type: "MultiChoice", choices: ["أفراد", "شركات"] },
];
const ANSWERS = { 0: "متجر عطور", 1: "logo.png", 2: "شركات" };

function newOrder(sell = 100, milestones?: Array<{ title: string; amount: number }>): ServiceOrder {
  const result = createOrder({
    catalogItemId: "item-1",
    offeringName: "Basic",
    partnerId: "p-svc",
    tenantId: "t-1",
    participationMode: "Principal",
    buy: 49,
    sellOrFee: sell,
    milestones,
    actor: "m-1",
    at: T0,
  });
  expect(result.error).toBeUndefined();
  return result.order!;
}

describe("service order lifecycle (FE mirror of the domain)", () => {
  it("full legal path appends ordered history; Accepted → Closed immediate but distinct", () => {
    const order = newOrder();
    expect(submitRequirements(order, REQS, ANSWERS, "m-1", at(0))).toBeNull();
    expect(partnerAccept(order, "p-1", at(1))).toBeNull();
    expect(markDelivered(order, "p-1", at(2))).toBeNull();
    expect(requestRevision(order, "m-1", "عدّل النص", at(3))).toBeNull();
    expect(markDelivered(order, "p-1", at(4))).toBeNull();
    expect(merchantAccept(order, "m-1", at(5))).toBeNull();

    expect(order.status).toBe("Closed");
    expect(order.revisionCount).toBe(1);
    expect(order.history.map((h) => h.action)).toEqual([
      "Created",
      "RequirementsSubmitted",
      "PartnerAccepted",
      "Delivered",
      "RevisionRequested",
      "Delivered",
      "MerchantAccepted",
      "Closed",
    ]);
    expect(order.history.map((h) => h.orderIndex)).toEqual([0, 1, 2, 3, 4, 5, 6, 7]);
    expect(order.history.find((h) => h.action === "RevisionRequested")!.note).toBe("عدّل النص");
  });

  it("illegal transitions and terminal immutability return the right guard errors", () => {
    expect(markDelivered(newOrder(), "p", T0)).toBe("orderErrIllegalTransition");

    const inProgress = newOrder();
    submitRequirements(inProgress, REQS, ANSWERS, "m", T0);
    partnerAccept(inProgress, "p", T0);
    expect(merchantAccept(inProgress, "m", T0)).toBe("orderErrIllegalTransition");
    expect(merchantCancel(inProgress, "m", T0)).toBe("orderErrIllegalTransition"); // only before InProgress
    expect(partnerDecline(inProgress, "p", "busy", T0)).toBe("orderErrIllegalTransition");

    const cancelled = newOrder();
    expect(merchantCancel(cancelled, "m", T0)).toBeNull();
    expect(submitRequirements(cancelled, REQS, ANSWERS, "m", T0)).toBe("orderErrTerminal");

    const declined = newOrder();
    submitRequirements(declined, REQS, ANSWERS, "m", T0);
    expect(partnerDecline(declined, "p", "", T0)).toBe("orderErrNoteRequired");
    expect(partnerDecline(declined, "p", "خارج النطاق", T0)).toBeNull();
    expect(partnerAccept(declined, "p", T0)).toBe("orderErrTerminal");
  });

  it("auto-accept boundary: 7d − 1s not due; exactly 7d due with System actor; idempotent", () => {
    const order = newOrder();
    submitRequirements(order, REQS, ANSWERS, "m", T0);
    partnerAccept(order, "p", T0);
    markDelivered(order, "p", at(1));

    const deliveredMs = new Date(at(1)).getTime();
    const justBefore = new Date(deliveredMs + 7 * 24 * 3600 * 1000 - 1000).toISOString();
    const exactly = new Date(deliveredMs + 7 * 24 * 3600 * 1000).toISOString();

    expect(autoAcceptIfDue(order, justBefore)).toBe(false);
    expect(order.status).toBe("Delivered");

    expect(autoAcceptIfDue(order, exactly)).toBe(true);
    expect(order.status).toBe("Closed");
    expect(order.history.find((h) => h.action === "AutoAccepted")!.actor).toBe(SYSTEM_ACTOR);

    const rows = order.history.length;
    expect(autoAcceptIfDue(order, at(20))).toBe(false); // idempotent
    expect(order.history.length).toBe(rows);
  });

  it("requirements: answer-per-requirement, typed caps, invalid MultiChoice, title snapshot survives edits", () => {
    expect(submitRequirements(newOrder(), REQS, { 0: "x", 2: "شركات" }, "m", T0)).toBe("orderErrAnswerMissing");
    expect(
      submitRequirements(newOrder(), REQS, { ...ANSWERS, 0: "x".repeat(151) }, "m", T0),
    ).toBe("orderErrAnswerTooLong");
    expect(submitRequirements(newOrder(), REQS, { ...ANSWERS, 2: "حكومة" }, "m", T0)).toBe("orderErrInvalidChoice");

    const order = newOrder();
    submitRequirements(order, REQS, ANSWERS, "m", T0);
    REQS[0].title = "نبذة (معدّلة لاحقًا)"; // listing edited later
    expect(order.answers[0].requirementTitle).toBe("نبذة"); // the order keeps what the merchant saw
    REQS[0].title = "نبذة"; // restore for other tests
    expect(order.answers.find((a) => a.requirementType === "FileUpload")!.answerText).toBe("logo.png");
  });

  it("merchant answers may carry a phone + email — the :056 policy does not apply to answers", async () => {
    // The same text is a CONTACT CHANNEL under the partner-content policy…
    const { containsContactChannel } = await import("@/lib/catalog/listingSchema");
    const contactAnswer = "تواصلوا معي على 0551234567 أو owner@mystore.sa";
    expect(containsContactChannel(contactAnswer)).toBe(true);

    // …but submits successfully as a merchant answer, stored verbatim.
    const order = newOrder();
    expect(submitRequirements(order, REQS, { ...ANSWERS, 0: contactAnswer }, "m", T0)).toBeNull();
    expect(order.status).toBe("RequirementsSubmitted");
    expect(order.answers[0].answerText).toBe(contactAnswer);
  });

  it("milestones: min price, row count, first ≥40%, Σ == price via cents arithmetic", () => {
    expect(validateMilestonePlan(999.99, [{ title: "أ", amount: 500 }, { title: "ب", amount: 499.99 }]))
      .toBe("orderErrMilestoneMinPrice");
    expect(validateMilestonePlan(1000, [{ title: "أ", amount: 1000 }])).toBe("orderErrMilestoneRowCount");
    expect(
      validateMilestonePlan(1200, Array.from({ length: 6 }, (_, i) => ({ title: `م${i}`, amount: 200 }))),
    ).toBe("orderErrMilestoneRowCount");
    expect(validateMilestonePlan(1000, [{ title: "أ", amount: 390 }, { title: "ب", amount: 610 }]))
      .toBe("orderErrMilestoneFirstShare");
    expect(validateMilestonePlan(1000, [{ title: "أ", amount: 400 }, { title: "ب", amount: 600 }])).toBeNull(); // 40% exact
    expect(validateMilestonePlan(1000, [{ title: "أ", amount: 400 }, { title: "ب", amount: 599.99 }]))
      .toBe("orderErrMilestoneSum");
    expect(validateMilestonePlan(1500, [
      { title: "أ", amount: 600 },
      { title: "ب", amount: 450 },
      { title: "ج", amount: 450 },
    ])).toBeNull();

    const order = newOrder(1500, [
      { title: "أ", amount: 600 },
      { title: "ب", amount: 450 },
      { title: "ج", amount: 450 },
    ]);
    expect(order.milestones.map((m) => m.orderIndex)).toEqual([0, 1, 2]);
  });

  it("snapshots are immutable copies — mode-per-offering pair", () => {
    const order = newOrder(100);
    expect(order.buySnapshot).toBe(49);
    expect(order.sellSnapshot).toBe(100);
    expect(order.feeSnapshot).toBeUndefined();

    const fee = createOrder({
      catalogItemId: "item-2",
      offeringName: "Sub",
      partnerId: "p",
      tenantId: "t",
      participationMode: "SubscriptionFee",
      sellOrFee: 49,
      actor: "m",
      at: T0,
    }).order!;
    expect(fee.feeSnapshot).toBe(49);
    expect(fee.buySnapshot).toBeUndefined();
    expect(fee.sellSnapshot).toBeUndefined();
  });
});

describe("scoped projections (6b structural standard, populated fixtures)", () => {
  function walkKeys(value: unknown, assertKey: (key: string, path: string) => void, path = "view"): void {
    if (value == null || typeof value !== "object") return;
    for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
      assertKey(key.toLowerCase(), `${path}.${key}`);
      walkKeys(child, assertKey, `${path}.${key}`);
    }
  }

  it("merchant view carries SELL/fee only; partner view carries BUY only; margin on neither", () => {
    const order = newOrder(1500, [
      { title: "أ", amount: 600 },
      { title: "ب", amount: 450 },
      { title: "ج", amount: 450 },
    ]);
    submitRequirements(order, REQS, ANSWERS, "m", T0); // POPULATED fixture

    const merchant = merchantOrderView(order);
    expect(merchant.priceAmount).toBe(1500);
    walkKeys(merchant, (key, path) => {
      expect(["buy", "buyamount", "buysnapshot", "margin"].includes(key), `forbidden ${path}`).toBe(false);
    });

    const partner = partnerOrderView(order);
    expect(partner.buyAmount).toBe(49);
    walkKeys(partner, (key, path) => {
      expect(
        ["sell", "sellsnapshot", "fee", "feesnapshot", "price", "priceamount", "margin", "milestones"].includes(key),
        `forbidden ${path}`,
      ).toBe(false);
    });
  });
});

describe("store + demo seed", () => {
  beforeEach(() => {
    stubLocalStorage();
    resetServiceOrders();
  });

  it("the demo seed carries InProgress, nearly-due Delivered, Closed, and a 40/30/30 plan order", () => {
    const tenantOrders = listOrdersForTenant("33333333-3333-3333-3333-333333333001");
    expect(tenantOrders).toHaveLength(4);
    expect(tenantOrders.map((o) => o.status).sort()).toEqual(
      ["Closed", "Delivered", "InProgress", "RequirementsSubmitted"].sort(),
    );

    const delivered = tenantOrders.find((o) => o.status === "Delivered")!;
    const dueInMs = new Date(delivered.deliveredAt!).getTime() + 7 * 24 * 3600 * 1000 - Date.now();
    expect(dueInMs).toBeGreaterThan(0); // not yet due…
    expect(dueInMs).toBeLessThan(2 * 24 * 3600 * 1000); // …but nearly (demoable)

    const planned = tenantOrders.find((o) => o.milestones.length > 0)!;
    expect(planned.milestones.map((m) => m.amount)).toEqual([600, 450, 450]);
    expect(planned.answers.some((a) => a.requirementType === "FileUpload")).toBe(true);
    expect(planned.answers.some((a) => a.requirementType === "MultiChoice")).toBe(true);

    // Partner sees the same orders through the partner scope.
    expect(listOrdersForPartner("22222222-2222-2222-2222-222222222004")).toHaveLength(4);
  });

  it("mutateOrder persists successful transitions and surfaces guard errors; sweep is idempotent", () => {
    const delivered = listOrdersForTenant("33333333-3333-3333-3333-333333333001")
      .find((o) => o.status === "Delivered")!;

    expect(mutateOrder(delivered.id, (o) => requestRevision(o, "m", "", T0))).toBe("orderErrNoteRequired");
    expect(mutateOrder(delivered.id, (o) => requestRevision(o, "m", "لون الشعار غير صحيح", new Date().toISOString()))).toBeNull();
    expect(
      listOrdersForTenant("33333333-3333-3333-3333-333333333001").find((o) => o.id === delivered.id)!.status,
    ).toBe("InProgress");

    // Sweep: nothing due anymore (the nearly-due order just went back to InProgress).
    expect(runAutoAcceptSweep(new Date().toISOString())).toBe(0);

    // Fast-forward: deliver again and sweep 8 days later — exactly one auto-accept, then zero.
    expect(mutateOrder(delivered.id, (o) => markDelivered(o, "p", new Date().toISOString()))).toBeNull();
    const in8Days = new Date(Date.now() + 8 * 24 * 3600 * 1000).toISOString();
    expect(runAutoAcceptSweep(in8Days)).toBe(1);
    expect(runAutoAcceptSweep(in8Days)).toBe(0); // idempotent
  });
});
