# Settlement Platform — Read-Only Audit

**Scope:** whole Zahy Partner Platform repo — backend `Zahy.Settlement` (+ related backend modules) and the React frontend (`frontend/partner-portal-web`).
**Date:** 2026-06-20.
**Mode:** READ-ONLY. No source file was edited, deleted, renamed, or migrated. This document is the only file created. Every "fix" below is a *recommendation only* — nothing was changed.

> Business model audited: one settlement engine, three flows (Principal full double-entry / ReflectionOnly no-VAT / SubscriptionFee-Fee VAT on Zahy's fee only); four-way split `Collected = Merchant + Partner/Delivery + ZahyMargin + NetVAT`; payer-selectable fees (Merchant→1200, Partner→1250) as config on the one engine; the seven report levels; and RBAC scope (PlatformAdmin/management = all, Accountant = all finance, PartnerFinance = own partner, Merchant = own).

## Legend

- **Status:** `present` / `missing` / `partial` / `duplicated`.
- **Severity:** `high` (blocks the business model or risks wrong money/scope), `med` (works but incomplete/confusing), `low` (cosmetic / cleanliness).
- **Effort:** `LIGHT` (localized, safe to do later) / `HEAVY` (architectural — **flag for human decision, do not auto-fix**).

Findings are reported, not ranked or fixed.

---

## 1. Accounting lifecycle coverage (order, end to end)

End-to-end chain: **capture → settlement journal → four-way split → reflection (partner + merchant) → fee (if any) → report → payment received → reconcile.**

| Step | Status | Where it lives | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| **Capture** (inbound order/txn) | present | `Zahy.Settlement.Application/SettlementWebhookIngestionService.cs:55-115` (verify→quarantine→dedupe→allocate→`Collected→Allocated`); HMAC `SettlementWebhookSignatureVerifier.cs:12-28`; dedupe unique index `ZahySettlementDbContext.cs:49`; idempotency key `SettlementCase.cs:38,109` | — | — | Solid. Note: only webhook capture exists; CDC path is absent (see §5). |
| **Settlement journal** (build) | present (compute-only) | `SettlementPostingTemplates.cs` Principal:17-50, Fee/SubscriptionFee:57-93, ReflectionOnly:96-97; `PostingResult.cs:71-103`; `VatMath.cs:10-30` | — | — | Balanced, code-based, well tested. Persistence gated OFF by design. |
| **Four-way split** | partial | Backend Marketplace: `SettlementAllocator.cs:33-75`, `SettlementAllocation.cs:30-45`, `SettlementAllocationSnapshot.cs:15-55`. Frontend: `lib/data/types.ts` `FourWaySplit:175-184`, `deriveFourWaySplit:625-647`, `assertSplitBalances:602-613` | med | HEAVY | Backend split is Marketplace-only (Integration book throws `AllocationNotConfiguredForBook`, `SettlementAllocator.cs:25-30`); split is computed twice (BE allocator vs FE `deriveFourWaySplit`). Decide one canonical split owner before go-live. |
| **Reflection → partner** | present | `ReflectedOrdersPage.tsx:134-163` (`ReflectionDetailPanel`), reachable via partner `commerce`/`fnb` module screens | — | — | Shows menu/list/delivery/customerPaid + Zahy fee. |
| **Reflection → merchant** | missing | `MerchantPreviewPage.tsx:14,149` only embeds `FinanceDrillDown`; `MerchantDetailPage.tsx:127-206` shows partners/activations, never reflection fields | med | LIGHT | Surface the same `ReflectionDetailPanel` (read-only, own-scope) in the merchant view. |
| **Fee (if any)** | present (compute-only) | `ActivationFeeComputer.cs:15-70` (subscription once/period, per-txn once/success), `SettlementPostingTemplates.Fee:67-93` payer-selectable; FE config `ActivationFeeMatrix.tsx`, `activationFees.ts` | — | — | Config + display only; posts no journal (per design). |
| **Report** | partial | FE: full single-source set in `lib/reports/settlementReports.ts` wired to UI. BE: `SettlementReports.cs:14-228` (all 7 levels) exist but are **test-only** — no HTTP/read-service exposes them (`SettlementReadController.cs:9-30` only serves cases/reversals/billing-charges) | med | HEAVY | Decide whether the 7 report levels get exposed via the backend read API or stay frontend-derived for the mock track. |
| **Payment received** | missing (settlement) / partial (invoices) | No settlement payment-received handler anywhere in `Zahy.Settlement` (no `PaymentReceived`/`remittance` matches). FE has invoice-level payments (`BillingPage.tsx:194-203`, `deriveInvoicePayment` `types.ts:753-770`) and a per-case collection panel (`SettlementPage.tsx:312-380`) | high | HEAVY | See §4 — settlement-level "payment received / settled to bank" is unrepresented; flag for human design. |
| **Reconcile** | missing | Enum value + permission only: `SettlementCaseState.Reconciled` `SettlementCaseState.cs:11`; `SettlementStateMachine.cs:19,29`; `ZahyPermissions.Settlement.Reconcile`. No code advances a case past `Allocated`; no reconcile service/UI | high | HEAVY | No case ever reaches Invoiced/Cleared/Disbursed/Reconciled. Flag for human design. |

**Lifecycle summary:** capture → journal → split → reflection(partner) → fee → report(frontend) is **present**; reflection(merchant), report(backend API), **payment received (settlement)** and **reconcile** are the broken/missing links — and the last two are intentional (engine flagged OFF), so they are gaps to *design*, not bugs to fix.

---

## 2. Order → Merchant (and Partner) mapping

| Check | Status | Where | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| Order carries partner + merchant tags | present | `PostingResult.Tag(partnerId, merchantId, period, orderRef, batchRef?)` `PostingResult.cs:54-69` | — | — | Every financial entry is taggable to both. |
| Order appears in **partner** statement | present | BE `SettlementReports.Partner` `SettlementReports.cs:124`; FE `partnerStatement` `settlementReports.ts:307` | — | — | Scoped by `PartnerId`. |
| Order appears in **merchant** statement | present | BE `SettlementReports.Merchant` `SettlementReports.cs:137`; FE `merchantStatement` `settlementReports.ts:327` | — | — | Scoped by `MerchantId`/`tenantId`. |
| Traceability of capture → case → merchant | partial | Capture (`SettlementWebhookIngestionService`) sets `PartnerId` on the case (`SettlementCase.cs`) but **not** a `MerchantId`; merchant tagging lives on `PostingResult.Tag` at posting time, not on the captured case | med | HEAVY | Decide where merchant identity is bound at capture (case has partner only today); affects merchant statements off live captures. |
| Reflection order → merchant statement | partial | Reflection entries are `ReflectionOnly` with `lines: []` (`settlementReports.ts:202-214`), so they correctly contribute count-only, never money — but they therefore never appear as line items in the merchant's *financial* statement | low | LIGHT | Expected by design (no VAT/no money); if a merchant should *see* its reflected orders, surface them as a non-financial list (see §3). |

---

## 3. Transaction reflection (partner AND merchant views, detail fields)

Detail fields = `merchantMenuPrice` / `partnerListPrice` / `deliveryFee` / `customerPaid` (+ optional `zahyFeeInclusive`, `successful`). FE type `lib/data/types.ts:243-256`; BE columns `ReflectionLog.cs` + migration `20260620200002_AddReflectionLogDisplayFields.cs`.

| Check | Status | Where | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| Reflection log entity (no VAT, no journal) | present | `ReflectionLog.cs:12-69`; `SettlementPostingTemplates.ReflectionOnly:96-97`; `SettlementPostingService.ReflectAsync` writes one row | — | — | Correct: non-posting, display-only. |
| Detail fields persisted | present | BE nullable cols `MenuPrice/PartnerListPrice/DeliveryFee/CustomerPaid` (`ReflectionLog.cs`, migration `...200002`); FE `ReflectionDetail` `types.ts:243-256` | — | — | Backend + frontend shapes match (drop-in ready). |
| **Partner view** shows detail | present | `ReflectedOrdersPage.tsx:134-163` renders all four + Zahy fee | — | — | Good. |
| **Merchant view** shows detail | missing | `MerchantPreviewPage.tsx` / `MerchantDetailPage.tsx` never render the fields or list ReflectionOnly orders for the tenant | med | LIGHT | Add a read-only, own-scope reflection list/panel to the merchant view (reuse `ReflectionDetailPanel`). |
| Drill-down shows reflection | partial | `FinanceDrillDown.tsx` shows only money-bearing lines + reconciliation; reflection (`lines: []`) never surfaces there | low | LIGHT | Intended (drill-down is financial); link out to a reflection list rather than embedding. |

---

## 4. Settlement + payment received

| Check | Status | Where | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| Collection/remittance modeling (per case) | present | FE `CollectionRemittance` `types.ts:194-205`, `buildCollectionJournal:666-728`, `SettlementPage.tsx CollectionPanel:312-380` (COD inbound / online outbound, gateway ref/status, deductions, net remitted) | — | — | Good per-case collection picture in the mock. |
| Invoice payment received (subscription billing) | present | FE `recordPayment`/`sendReceipt`/`getReceipts` (`IPortalDataSource.ts:119-127`, `mockDataSource.ts:867-924`); UI `BillingPage.tsx:194-203,322-376`, `BalancesPage.tsx:26-166`; receipt PDF `lib/pdf/receiptPdf.ts` | — | — | Fully wired for invoices/balances. |
| **Settlement "payment received / settled to bank"** | missing | No `PaymentReceived`/`remittance` concept in `Zahy.Settlement` (whole-module search empty). `1100 Bank/Cash Clearing` is declared/seeded (`SettlementAccountCode.cs:13`, `SettlementChartOfAccounts.cs:14`) but **never debited/credited** by any posting path | high | HEAVY | Business model's "payment received" has no backend home at the settlement level. Flag for human design (where funds land, which 1100 posting, which state transition). |
| Disbursement / payout execution | missing (flag + naming only) | `SettlementEngineOptions.DisbursementEnabled=false` `SettlementEngineOptions.cs:11`; FE disburse button permanently `disabled` (`SettlementPage.tsx:288-292`, `disburseDisabledDemo`); `Disburse.Execute` permission `portalRoles.ts:28` | high | HEAVY | No payout run / remittance batch. Intentional pre-sign-off; flag for design. |
| Reconcile | missing | `Settlement.Reconcile` permission defined but never consulted; no case advances past `Allocated` (no caller of `TransitionTo(Invoiced/Cleared/Disbursed/Reconciled)` or `.Advance()`) | high | HEAVY | Reconciliation step is undesigned. Flag for human decision. |

**Confirmed:** payment-received at the **settlement** layer is missing (as the prompt anticipated). Invoice/subscription payment-received is present and complete in the frontend mock.

---

## 5. Missing items (business-model needs with no code home)

| Item | Status | Evidence | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| Settlement payment-received / settled-to-bank posting (1100) | missing | `1100` declared, seeded, tested, but zero usages in any template/allocator | high | HEAVY | Design the cash-clearing posting + the state it triggers. |
| Reconciliation service + case advancement past `Allocated` | missing | states/permission exist; no executing code | high | HEAVY | Design the Invoiced→Cleared→Disbursed→Reconciled progression and who triggers each. |
| Backend report API for the 7 levels | missing | `SettlementReports.*` is test-only; controller exposes only cases/reversals/billing-charges (`SettlementReadController.cs:9-30`) | med | HEAVY | Decide: expose backend reports, or keep frontend-derived for the mock. |
| Merchant-side reflection visibility | missing | merchant pages omit reflection fields | med | LIGHT | Add own-scope reflection panel to merchant view. |
| CDC capture path (per architecture: read-only SQL CDC → owned Order Ledger) | missing in `Zahy.Settlement` | only webhook capture exists; CDC refs live only in `Zahy.OrderLedger` | med | HEAVY | Confirm whether settlement should also ingest via CDC, or webhooks-only is intended here. |
| Merchant identity bound at capture | missing | captured `SettlementCase` carries `PartnerId` only; merchant tagging is at posting time | med | HEAVY | Decide where merchant id is captured for live merchant statements. |
| `2300 VatControl` / `2400 ReflectionClearing` usage | missing (declared, seeded, unused) | `SettlementAccountCode.cs:21-22`; no posting references them | low | LIGHT | Expected (period-close / pass-through not yet implemented); leave until those flows exist. |
| Central VAT-rate constant | missing | `0.15` literal duplicated as default in ~5 functions across `types.ts` + `activationFees.ts` | low | LIGHT | Introduce one `VAT_RATE` constant to avoid drift (a guard test already blocks `*0.15`/`/1.15` in finance card files). |

---

## 6. Duplicates (two pieces doing the same job)

| Pair | Status | Where | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| **Legacy account enum vs chart codes** (two account representations) | duplicated (bridged, not migrated) | `SettlementAccountType.cs` (enum, used by `SettlementAllocator.cs`, `SettlementAllocationSnapshot.cs`, `PrincipalResaleJournalBuilder.cs`) vs `SettlementAccountCode.cs` (codes, used by `SettlementPostingTemplates.cs`); bridge `SettlementAccountTypeCodeMap.cs:13-33`. Read API reparses enum strings (`SettlementReadAppService.cs:74-79`) | med | HEAVY | Pick chart codes as the single representation and migrate allocator/snapshot/builder; flag (touches posting + persisted snapshots). |
| **Net-VAT/margin: `deriveSettlementSummary` vs `settlementReports`** | duplicated | FE per-case `deriveSettlementSummary` `types.ts:503-533` (used by `chartAnalytics.ts:96,163`, `SettlementPage.tsx:194`, `settlementExport.ts`) computes net VAT/margin independently of `vatControl`/`platformTotals` (`settlementReports.ts:301,269`) | med | LIGHT/HEAVY | Settlement page/export/analytics bypass the single-source reports. Route them through `settlementReports` (light per call site; heavy if analytics is reworked). |
| **Analytics KPI engine vs reports engine** | duplicated | `chartAnalytics.ts:86-217` builds KPIs/trends from raw `PortalData`; finance cards/Reports use `settlementReports.ts` | med | HEAVY | Two engines read the same dataset; consolidate or clearly delineate "charts vs ledger". |
| **Inclusive→net+VAT formula** repeated | duplicated | `types.ts:555-558`, `:636-639`, `:682-685`; `activationFees.ts:21-24` | low | LIGHT | Factor one `splitInclusive(amount, rate)` helper. |
| **Reports nav appears twice** (sidebar + finance tab) | duplicated (intentional deep-link) | sidebar `nav.ts:107-116` and finance tab `partnerModules.ts:224-225` both → `/finance/reports` | low | LIGHT | When inside Finance, the link shows twice; consider hiding the sidebar entry while on `/finance/*` or vice-versa. |
| `SettlementEngineOptions.cs` "twice" in glob | not a duplicate | one physical file; Windows slash quirk in the index | — | — | No action. |

---

## 7. Misplaced / orphaned / parked

| Item | Status | Where | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| Consignment / FBA module | parked (by design) | FE `partnerModules.ts:129-135` `comingSoon:true`, `ModuleComingSoonPage.tsx`, `Partner.parkedModule` `types.ts:59-60`; BE routing parked + flagged off `ConsignmentSettlementOptions.SettlementDispatchEnabled=false`, `ConsignmentSaleSettlementRouter.cs:38-60` | low | — | Parked, not a bug. Leave until go-live. |
| F&B module screens | parked-ish | `fnb` not `comingSoon` but `PosSyncPage.tsx:6-25`, `SnapshotsPage.tsx:6-29`, partial `MenuPage.tsx` are placeholders (`mockSampleNotice`) | low | — | Placeholder content; note as parked. |
| `SettlementReports.*` (backend) | orphaned (test-only) | referenced only under `test/`; no production caller | med | HEAVY | Either expose via read API or mark explicitly as frontend-parity reference. |
| `1100`/`2300`/`2400` chart codes | orphaned (declared/seeded/unused) | `SettlementAccountCode.cs:13,21-22` | low | LIGHT | Expected pre-implementation; leave. |
| Integration-book allocation | parked | `SettlementAllocator.cs:25-30` throws `AllocationNotConfiguredForBook` ("⚠️ PROVISIONAL") | med | HEAVY | Parked pending chart sign-off; flag. |
| `ZahySettlementDbSchemaMigrator` not wired to DbMigrator | intentional | `ZahySettlementDbSchemaMigrator.cs:14-18` ("never invoked against any real database") | — | — | Matches guardrails; leave. |
| Provisional leg mappings | parked (acknowledged) | `SettlementAllocator.cs:14-17`, `PrincipalResaleJournalBuilder.cs:10`, `SettlementAccountType.cs:5` ("pending accountant sign-off") | med | HEAVY | Reconcile with chart sign-off; flag. |
| No `TODO`/`NotImplementedException`/`JUMP`/`FBA` posting stubs | clean | none found in `src/Zahy.Settlement` or FE `src/` | — | — | No dead markers. |

---

## 8. UI usability

| Observation | Status | Where | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| Tab/subtab active-state cues | present | shared `lib/ui/tabs.ts:1-17` (`tabLinkClass`) used by Finance tabs (`FinanceWorkspacePage.tsx:30-47`), `ModuleSubNav.tsx`, `PartnerSubNav.tsx`, Settings; sidebar `AdminShell.tsx:169-186` | low | — | Consistent and clear. |
| Reports link duplicated (sidebar + tab) | duplicated | see §6 | low | LIGHT | Avoid showing both simultaneously to reduce "which one?" confusion. |
| "Same screen, two contexts" (Settlement/Reversals/Billing/Reports under both `/finance/*` and `/modules/{module}/*`) | partial | `AdminRoutes.tsx:251-256` aliases; `moduleScreenRegistry.tsx` | med | LIGHT | Sidebar doesn't indicate which context is active; add a context breadcrumb/heading. |
| Disburse button permanently disabled | present (intentional) | `SettlementPage.tsx:288-292` (`disburseDisabledDemo`) | low | — | Tooltip explains; fine for demo. |
| Missing empty states | partial | `ReflectedOrdersPage.tsx:88-124`, `CatalogPage.tsx:70-83`, `MenuPage.tsx:44-56`, `ActivationsPage.tsx:76-139`, `BillingPage.tsx:174-213` render empty tables with no "nothing here" message | low | LIGHT | Add empty-state messages (several other pages already have them). |
| Merchant reflection blind spot | missing | merchant cannot see their reflected-order detail (see §3) | med | LIGHT | A merchant user may be "lost" — they see finance drill-down but not the order story. |
| Activation multi-step flow | present | `ActivationsPage.tsx:89-121` stage-gated buttons; `MerchantDetailPage.tsx:29-53` pipeline pills | low | — | Clear stepper. |
| Loading states | present | spinners across Finance/Billing/Settlement/Reports/Merchants pages | low | — | Good coverage. |

---

## 9. Invariants still holding

| Invariant | Status | Evidence | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| **DR = CR** on every journal | present | `PostingResult.Financial` rejects unbalanced/≥2-leg/single-currency `PostingResult.cs:71-103`; FE journals assert balance | — | — | Enforced at construction. |
| **Trial balance nets to zero** | present | BE `SettlementReports.TrialBalanceFor` + `ReflectionDisplayAndTrialBalanceTests.cs`; FE `trialBalance` `settlementReports.ts:262` | — | — | Verified by tests. |
| **Net VAT = 2200 − 1300** | present | BE `SettlementReports.VatControl` `SettlementReports.cs:180`; FE `vatControl`/`platformTotals` `settlementReports.ts:301,290` (`outputVat − inputVat`) | — | — | Single-source on both sides. Caveat: FE `deriveSettlementSummary` computes VAT separately (see §6) — same formula, parallel path. |
| **Four-way split balances** | present | FE `assertSplitBalances` `types.ts:602-613`; BE allocator validates `sum == collected` `SettlementAllocator.cs:46` | — | — | Holds where computed (Marketplace BE; FE). |
| **PostingEnabled OFF** | present | `SettlementEngineOptions.PostingEnabled=false` `SettlementEngineOptions.cs:19`; `SettlementPostingFeatureFlagTests.cs:9-12` | — | — | Confirmed off by default + test-guarded. |
| **No live wiring** (no disbursement / live provider / DbMigrator) | present | `DisbursementEnabled=false`/`LiveProviderEnabled=false` `SettlementEngineOptions.cs:11,13`; schema migrator unwired `ZahySettlementDbSchemaMigrator.cs:14-18`; FE disburse disabled | — | — | All real-world execution OFF, matching guardrails. |
| **Reflection contributes 0 to financials** | present | `ReflectionOnly` has no lines (`SettlementPostingTemplates.cs:96-97`, FE `settlementReports.ts:202-214`); test `ReflectionDisplayAndTrialBalanceTests.cs` | — | — | Count-only, no VAT/margin impact. |

**All listed invariants hold.** The only nuance: net-VAT is computed by a single source for the finance cards/reports, but the Settlement page/export/analytics recompute per-case figures via `deriveSettlementSummary` (same arithmetic, separate code path) — a duplication risk, not a broken invariant.

---

## Closing

- **Nothing was changed.** No edits, deletes, refactors, renames, or migrations were performed. Only this report (`docs/SETTLEMENT-AUDIT.md`) was created.
- **Headline gaps (all HEAVY → human decision):** settlement-level *payment received* / *reconcile* / *disbursement* are unrepresented (intentionally flagged OFF); backend `SettlementReports` are test-only (not API-exposed); legacy-enum vs chart-code account representations coexist; the four-way split and net-VAT have a parallel frontend computation path.
- **Light touches available:** merchant-side reflection visibility, missing empty states, central VAT-rate constant, and de-duplicating the Reports nav entry.
- **Invariants:** DR=CR, trial balance = 0, net VAT = 2200−1300, PostingEnabled OFF, and no live wiring — **all holding**.
