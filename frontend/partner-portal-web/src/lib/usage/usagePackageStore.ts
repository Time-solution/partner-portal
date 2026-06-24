import {
  DEFAULT_USAGE_PACKAGE_UNIT,
  normalizeUsagePackageInput,
  type UsagePackage,
  type UsagePackageInput,
} from "./usagePackage";

/**
 * U2 — sibling localStorage store for usage packages (mock), mirroring the `usageStore` / `bankRegistryStore`
 * pattern. CONFIG ONLY: create / edit / publish / archive a pricing package. NO billing is computed and NO
 * journal is posted here (that is the gated U3 phase).
 */

// Bumped v2 -> v3 (Phase 6a) to re-seed with partner-authored packageExplanation on demo packages.
const STORAGE_KEY = "zahy-usage-packages-v3";

// Demo IDs — mirror the fixtures (WhatsApp Co / W there).
const PARTNER_WHATSAPP = "22222222-2222-2222-2222-222222222004";
const PARTNER_WTHERE = "22222222-2222-2222-2222-222222222008";

/**
 * Seeded demo packages so EVERY billing shape is testable per partner/merchant/admin login:
 * resale flat, subscription flat, resale tiered (graduated), subscription tiered, pure per-use,
 * plus one Draft to prove drafts are hidden from merchant browse. Multiple packages per partner.
 * CONFIG ONLY — no billing computed, no journal posted (compute happens in the gated U3 phase).
 */
function seedDefaults(): UsagePackage[] {
  return [
    // ── WhatsApp Co (service partner) ───────────────────────────────────────────
    // 1) RESALE FLAT — base + included + flat overage (buy/sell pair).
    {
      id: "pkg-wa-resale-flat",
      partnerId: PARTNER_WHATSAPP,
      name: "WhatsApp Resale Flat 5k",
      unitLabel: "messages",
      mode: "Resale",
      currency: "SAR",
      includedQuantity: 5000,
      baseBuyAmount: 150,
      baseSellAmount: 250,
      overageBuyAmount: 0.03,
      overageSellAmount: 0.05,
      payer: "Merchant",
      status: "Published",
      packageExplanation:
        "يشمل ٥٬٠٠٠ رسالة شهريًا، وتُحتسب الرسائل الإضافية بسعر الوحدة المبيّن. مناسب للمتاجر متوسطة الحجم.",
    },
    // 2) RESALE TIERED — graduated overage ladder: 0–10,000 @0.05 sell, beyond @0.04 (buy pair below).
    //    With included 5,000 + 16,200 usage -> overage 11,200 -> 10,000×0.05 + 1,200×0.04 = 548 sell.
    {
      id: "pkg-wa-resale-10k",
      partnerId: PARTNER_WHATSAPP,
      name: "WhatsApp Resale Tiered",
      unitLabel: "messages",
      mode: "Resale",
      currency: "SAR",
      includedQuantity: 5000,
      baseBuyAmount: 200,
      baseSellAmount: 300,
      overageBuyAmount: 0.04,
      overageSellAmount: 0.05,
      payer: "Merchant",
      status: "Published",
      tiers: [
        { fromQuantity: 0, toQuantity: 10000, buyRate: 0.04, sellRate: 0.05 },
        { fromQuantity: 10000, toQuantity: null, buyRate: 0.03, sellRate: 0.04 },
      ],
    },
    // 3) SUBSCRIPTION FLAT — fixed fee + included + flat overage fee (payer set).
    {
      id: "pkg-wa-sub-flat",
      partnerId: PARTNER_WHATSAPP,
      name: "WhatsApp Subscription Flat",
      unitLabel: "messages",
      mode: "Subscription",
      currency: "SAR",
      includedQuantity: 5000,
      baseBuyAmount: 0,
      baseSellAmount: 149,
      overageBuyAmount: 0,
      overageSellAmount: 0.03,
      payer: "Merchant",
      status: "Published",
    },
    // 4) DRAFT pure per-use — proves drafts are HIDDEN from merchant browse / not selectable.
    {
      id: "pkg-wa-payg",
      partnerId: PARTNER_WHATSAPP,
      name: "WhatsApp pay-as-you-go (draft)",
      unitLabel: "messages",
      mode: "Resale",
      currency: "SAR",
      includedQuantity: 0,
      baseBuyAmount: 0,
      baseSellAmount: 0,
      overageBuyAmount: 0.03,
      overageSellAmount: 0.06,
      payer: "Merchant",
      status: "Draft",
    },
    // ── W there (service partner) ───────────────────────────────────────────────
    // 5) RESALE FLAT — base + included + flat overage.
    {
      id: "pkg-wt-resale-flat",
      partnerId: PARTNER_WTHERE,
      name: "Wthere Resale Flat 4k",
      unitLabel: "messages",
      mode: "Resale",
      currency: "SAR",
      includedQuantity: 4000,
      baseBuyAmount: 100,
      baseSellAmount: 180,
      overageBuyAmount: 0.03,
      overageSellAmount: 0.05,
      payer: "Merchant",
      status: "Published",
      packageExplanation: "باقة ٤٬٠٠٠ رسالة شهريًا مع سعر ثابت للوحدة الإضافية.",
    },
    // 6) SUBSCRIPTION TIERED — graduated fee ladder on the overage (first 1k then beyond).
    {
      id: "pkg-wt-sub",
      partnerId: PARTNER_WTHERE,
      name: "Wthere Subscription Tiered",
      unitLabel: "messages",
      mode: "Subscription",
      currency: "SAR",
      includedQuantity: 3000,
      baseBuyAmount: 0,
      baseSellAmount: 149,
      overageBuyAmount: 0,
      overageSellAmount: 0.03,
      payer: "Merchant",
      status: "Published",
      tiers: [
        { fromQuantity: 0, toQuantity: 1000, buyRate: 0, sellRate: 0.05 },
        { fromQuantity: 1000, toQuantity: null, buyRate: 0, sellRate: 0.03 },
      ],
    },
    // 7) PURE PER-USE — base 0, included 0, rate only (Published, so it's selectable).
    {
      id: "pkg-wt-payg",
      partnerId: PARTNER_WTHERE,
      name: "Wthere Pay-as-you-go",
      unitLabel: "messages",
      mode: "Resale",
      currency: "SAR",
      includedQuantity: 0,
      baseBuyAmount: 0,
      baseSellAmount: 0,
      overageBuyAmount: 0.04,
      overageSellAmount: 0.06,
      payer: "Merchant",
      status: "Published",
    },
  ];
}

function loadList(): UsagePackage[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return seedDefaults();
    const parsed = JSON.parse(raw) as UsagePackage[];
    return Array.isArray(parsed) ? parsed : seedDefaults();
  } catch {
    return seedDefaults();
  }
}

function saveList(list: UsagePackage[]): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
}

function uid(): string {
  return `pkg-${Math.random().toString(36).slice(2, 10)}`;
}

export function listUsagePackages(partnerId?: string): UsagePackage[] {
  const all = loadList();
  return (partnerId ? all.filter((p) => p.partnerId === partnerId) : all)
    .slice()
    .sort((a, b) => a.name.localeCompare(b.name));
}

function ensureNonNegative(input: UsagePackageInput): void {
  const amounts = [
    input.includedQuantity,
    input.baseBuyAmount,
    input.baseSellAmount,
    input.overageBuyAmount,
    input.overageSellAmount,
  ];
  if (amounts.some((a) => a < 0)) {
    throw new Error("Usage package amounts cannot be negative");
  }
}

export function createUsagePackage(input: UsagePackageInput): UsagePackage {
  ensureNonNegative(input);
  const norm = normalizeUsagePackageInput(input);
  const pkg: UsagePackage = {
    id: uid(),
    partnerId: norm.partnerId,
    name: norm.name.trim(),
    unitLabel: (norm.unitLabel || DEFAULT_USAGE_PACKAGE_UNIT).trim(),
    mode: norm.mode,
    currency: (norm.currency || "SAR").trim(),
    includedQuantity: norm.includedQuantity,
    baseBuyAmount: norm.baseBuyAmount,
    baseSellAmount: norm.baseSellAmount,
    overageBuyAmount: norm.overageBuyAmount,
    overageSellAmount: norm.overageSellAmount,
    payer: norm.payer ?? "Merchant",
    status: "Draft",
    tiers: norm.tiers ?? [],
  };
  saveList([...loadList(), pkg]);
  return pkg;
}

export function updateUsagePackage(id: string, input: UsagePackageInput): UsagePackage {
  ensureNonNegative(input);
  const list = loadList();
  const idx = list.findIndex((p) => p.id === id);
  if (idx < 0) throw new Error("Usage package not found");
  const norm = normalizeUsagePackageInput({ ...input, mode: list[idx].mode });
  const updated: UsagePackage = {
    ...list[idx],
    name: norm.name.trim(),
    unitLabel: (norm.unitLabel || list[idx].unitLabel).trim(),
    includedQuantity: norm.includedQuantity,
    baseBuyAmount: norm.baseBuyAmount,
    baseSellAmount: norm.baseSellAmount,
    overageBuyAmount: norm.overageBuyAmount,
    overageSellAmount: norm.overageSellAmount,
    payer: norm.payer ?? list[idx].payer,
    tiers: norm.tiers ?? [],
  };
  const next = list.slice();
  next[idx] = updated;
  saveList(next);
  return updated;
}

function setStatus(id: string, status: UsagePackage["status"]): UsagePackage {
  const list = loadList();
  const idx = list.findIndex((p) => p.id === id);
  if (idx < 0) throw new Error("Usage package not found");
  if (status === "Published" && list[idx].status === "Archived") {
    throw new Error("Archived package cannot be published");
  }
  const updated = { ...list[idx], status };
  const next = list.slice();
  next[idx] = updated;
  saveList(next);
  return updated;
}

export function publishUsagePackage(id: string): UsagePackage {
  return setStatus(id, "Published");
}

export function archiveUsagePackage(id: string): UsagePackage {
  return setStatus(id, "Archived");
}

export function resetUsagePackages(): void {
  localStorage.removeItem(STORAGE_KEY);
}
