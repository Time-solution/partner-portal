# Zahy Settlement Engine — Design (Phase 0)

**Status:** PROPOSAL — awaiting CTO approval before any production code (Phase 1).
**Scope:** multi-party settlement, double-entry ledger, cost/markup, configurable VAT (agent vs principal), immutable ZATCA invoice store. All real-world execution stays **disabled behind feature flags** (no disbursement, no live ZATCA, no live aggregator).
**Working location:** main worktree on `feat/partner-merchant-finance` (where the code lives). This doc is the only file written in Phase 0.

> ⚠️ This design has **blocking open decisions** (rounding policy + agent/principal mapping + invoicing parties) listed in §11. The Phase-4 worked-example tests cannot be finalized until §11.1 is answered, because the spec's own shipping numbers are internally inconsistent (see §9).

---

## 1. What exists today (grounding)

- **One-sided fee ledger only.** `FinancePostingIngestionService` writes `AccountPosting` rows for the platform's commission and billing charges — never a counterparty leg, never a balanced journal. ([FinancePostingIngestionService.cs:58,105](../../src/Zahy.Finance/Zahy.Finance.Application/Postings/FinancePostingIngestionService.cs))
- **No cost side.** `OrderRecord.DeliveryFee` is a single flat decimal; carrier `RateAmount` is a single number. No buy/sell pair anywhere.
- **VAT hardcoded.** `taxAmount = FinanceMoney.RoundPosting(taxExclusive * 0.15m)` on the fee posting sum, platform as seller. ([FinanceInvoiceGenerationService.cs:208](../../src/Zahy.Finance/Zahy.Finance.Application/Invoicing/FinanceInvoiceGenerationService.cs:208))
- **ZATCA stubbed.** `LocalZatcaInvoiceShaper` builds a DTO; `NullZatcaSubmitter` no-ops; QR is a literal placeholder.
- **Rounding helpers exist** (`CommissionMoney`, `FinanceMoney`) — both `MidpointRounding.AwayFromZero`, 2dp.
- **PartnerType** = Aggregator, ThreePL, Carrier, Service, Marketplace ([PartnerType.cs](../../src/Zahy.PartnerPlatform/Zahy.PartnerPlatform.Domain.Shared/Partners/PartnerType.cs)).
- **RBAC** roles/permissions already structured (see §7).

**Baseline test count (this session, full run):** 190 backend passing across 8 projects + 13 frontend = **203 / 0 failed / 0 skipped**. Phase 0 changes no production code, so this is unchanged; I will re-run `dotnet test` immediately before and after Phase 1 and report deltas.

---

## 2. Module layout — new bounded context `Zahy.Settlement`

Separate module, separate DbContext, separate schema (table prefix `Stl*`), following existing ABP layering. It **consumes** results from Commission/OrderLedger via contracts; it does **not** duplicate commission math or mutate Finance.

```
src/Zahy.Settlement/
├─ Zahy.Settlement.Domain.Shared/        Money VO, enums, account chart, VAT treatment, consts, error codes
├─ Zahy.Settlement.Domain/               Journal, JournalLine, SettlementCase (aggregate+state machine),
│                                          LedgerAccount, CostMarkupLine; balance invariant lives here
├─ Zahy.Settlement.Application.Contracts/ command/query DTOs, ISettlementEngine, IVatCalculator,
│                                          ISettlementAccountResolver, IZatcaInvoiceStore, IDocumentArchive
├─ Zahy.Settlement.Application/           engine, allocators, VAT calculators (agent/principal),
│                                          posting services, flagged-off boundaries (disburse/zatca/archive)
├─ Zahy.Settlement.EntityFrameworkCore/   StlSettlementDbContext + migrations (generated as files only)
└─ Zahy.Settlement.HttpApi/               controllers, all gated by RBAC (§7)
```

**Why a new context (not extend Finance):** Finance is pending finance review and models partner/merchant fee accounts. Settlement introduces balanced journals and counterparty liabilities — a different aggregate with stricter invariants. Keeping it separate preserves the existing 203 green tests and lets the accountant view settlement books in isolation (mandatory per requirement 4). Integration to Finance/Commission is via the same **null-object bridge pattern** already used (`ZahyOrderLedgerWebhooksModule` etc.), so nothing wires up until co-hosted and flagged on.

---

## 3. Double-entry ledger primitives

- **`Money`** (value object, Domain.Shared): `{ decimal Amount; string Currency; bool VatInclusive }`. Centralizes rounding (replaces ad-hoc `CommissionMoney`/`FinanceMoney` calls inside settlement). Arithmetic guarded for same-currency; rounding policy per §9/§11.1.
- **`LedgerAccount`** identified by `(Book, AccountType)` where `Book` = partner-type book (§6) and `AccountType` = the chart enum (§4). Each account has a **normal balance** (Debit or Credit).
- **`JournalLine`** = `{ AccountType, Direction(Debit|Credit), Money }`.
- **`Journal`** (aggregate root, append-only) = header `{ Id, Book, SettlementCaseId, ExternalTxnId, IdempotencyKey, PostedAt, SourceType }` + immutable `IReadOnlyList<JournalLine>`.
- **Balance invariant** (enforced in the `Journal` factory, not the DB): `Σ debits == Σ credits` per currency, both > 0, ≥ 2 lines; throws `UnbalancedJournal` otherwise. Unit-tested first (Phase 1). Journals are never updated/deleted; corrections are **reversing journals** only.

---

## 4. Account chart (the named accounts)

`SettlementAccountType` enum + normal balance. Initial chart (final set pending accountant — §11):

| Account | Normal balance | Meaning |
|---|---|---|
| `AggregatorClearing` | Debit | Funds collected by aggregator, held/owed to us until disbursed/reconciled |
| `MerchantPayable` | Credit | Money **owed to the merchant** (the payout liability) |
| `PartnerPayable` | Credit | Money owed to a partner (service/integration payout) |
| `DeliveryCost` | Debit | Cost of fulfilment/carrier (input side) |
| `PlatformCommissionRevenue` | Credit | Platform's commission income |
| `ShippingMarginRevenue` | Credit | Margin on resold shipping (sell − buy, net) |
| `VatOutput` | Credit | Output VAT we charge (liability to ZATCA) |
| `VatInput` | Debit | Input VAT we reclaim (asset vs ZATCA) |

Net VAT to ZATCA = `VatOutput − VatInput` (control-account balance). See §9 for how that figure is derived vs the spec.

---

## 5. State machine + idempotency

`SettlementCase` aggregate drives:

```
Collected ──allocate──▶ Allocated ──invoice──▶ Invoiced ──clear──▶ Cleared ──disburse──▶ Disbursed ──reconcile──▶ Reconciled
```

- Each transition is a **command** with its own RBAC gate (§7) and emits exactly one balanced journal (or none for pure status moves).
- **Idempotency:** every inbound settlement event carries an `ExternalTxnId`; the engine dedupes on `(Book, ExternalTxnId, transition)` via a unique index + pre-check (same pattern as `FinancePostingIdempotency`). Replaying a processed event returns the prior result and posts nothing. Illegal transitions throw (mirrors `PartnerLifecyclePolicy`).
- `Disburse` and `Clear` (ZATCA) call **flagged-off boundaries** (§8) — they advance state and post journals but perform no real external action until enabled.

---

## 6. Cost + markup, and per-partner-type isolation

- **Cost/markup:** wherever a thing is resold, store **both** sides as `Money`: `BuyPrice` and `SellPrice` (+ `VatInclusive`). Applies to: partner service line, shipping/delivery line, item line. `CostMarkupLine` VO computes `margin = netSell − netBuy`. The existing single `DeliveryFee`/`RateAmount` become the *sell* side; the *buy* (carrier cost) side is new.
- **Per-partner-type books:** `Book` is a first-class dimension on every account, journal, and settlement case. Initial mapping (configurable, see §11.2):
  - **Model 2 / Marketplace (Aggregator):** item-settlement flow — collected total split into MerchantPayable + DeliveryCost + PlatformCommissionRevenue + AggregatorClearing.
  - **Model 3 / Integration (Service):** service/shipping resale flow — cost+markup with PartnerPayable + ShippingMarginRevenue.
  - Separate account trees + journals per book ⇒ the accountant queries one book in isolation. Cross-book postings are forbidden by invariant.

### 6.1 Flow profiles = composition, not inheritance (Addition A — confirmed against §4/§5)
One **shared** settlement engine (the Phase-1 double-entry core). Each partner type is a **flow profile** selected by strategy, **never** subclassing:

```csharp
public interface ISettlementFlowProfile            // resolved per SettlementBook
{
    SettlementBook Book { get; }                   // Marketplace (Aggregator) | Integration (Service)
    IReadOnlyCollection<SettlementAccountType> AccountTree { get; }   // the subset of §4 this book uses
    SettlementCase StartCase(...);                 // its own state-machine instance (§5)
    AllocationResult Allocate(SettlementInput input);                // produces the balanced legs to post
}
```

- **No `AggregatorSettlement : BaseSettlement`.** The shared engine owns the primitives (Money, Journal, balance invariant, state-machine mechanics, idempotency); profiles are injected strategies that own *their* account subtree, *their* journal stream, and *their* state-machine instance.
- Isolation is therefore structural: a profile can only touch accounts in its own `AccountTree`/`Book`; cross-book postings remain invariant-forbidden. The accountant follows each partner type independently.
- This **matches** §4/§5/§6 as written — it specifies the *how* (strategy composition) without changing the account chart, the state machine, or the `Book` dimension. No adjustment to those sections needed.

---

## 7. RBAC (reuse existing model)

Add a `Settlement` permission group + scope `settlement:read`/`settlement:manage`, and map to existing roles in `ZahyRoleRegistry`:

| Requested role | Maps to / becomes | Settlement rights |
|---|---|---|
| **PlatformAdmin** | `PlatformSuperAdmin` (has `ZahyPermissions.All()`) | all commands incl. disburse |
| **Accountant** | extend `PlatformFinance` | read **all** books + `Reconcile`; **no** `Disburse` |
| **PartnerFinance** | `PartnerOwner`/`PartnerManager` (partner-scoped via `PartnerId` claim) | read **own partner + own partner-type book only** |

Every settlement command and every ledger/invoice query is `[Authorize]`-gated and additionally filtered by `Book` + `PartnerId` (reusing the existing partner/tenant query-filter approach in `ZahyFinanceDbContext`).

---

## 8. Feature flags (all real execution OFF)

Config keys (values default false; never read/print connection strings):

- `Zahy:Settlement:Disbursement:Enabled` → real fund movement. Off ⇒ `NullDisbursementGateway`.
- `Zahy:Settlement:Zatca:Submission:Enabled` → live clearance/reporting. Off ⇒ keep `NullZatcaSubmitter` semantics behind `IZatcaClearanceClient`.
- `Zahy:Settlement:Aggregator:Live` → real aggregator calls. Off ⇒ in-memory transport (matches today's connector posture).
- `Zahy:Settlement:Archive:Live` → real bucket for cleared XML. Off ⇒ `InMemoryDocumentArchive` behind `IDocumentArchive`.
- VAT rate + agent/principal flags: config-driven (§9), **no hardcoded 0.15**.

---

## 9. VAT engine (configurable) + the rounding problem

VAT rate from config (`Zahy:Settlement:Vat:StandardRate`, default 0.15 but overridable). Per-partner-type `VatTreatment` flag selects:

- **AGENT:** output VAT on platform commission/margin only (today's behaviour, generalized).
- **PRINCIPAL / RESALE:** output VAT on full **sell** net; input VAT reclaimed on **buy** net; net-to-ZATCA = output − input.

**Negative-VAT guard:** in a payout-dominant period, output−input must not produce a negative tax on a tax invoice. Proposed handling: clamp the *invoice's* VAT line at ≥ 0 and carry the excess input VAT as a reclaim/credit on the period return — **exact treatment is an accountant decision (§11.4)**; the guard itself (never emit negative VAT on a standard tax invoice) is non-negotiable.

### 9.1 ⚠️ Worked-example arithmetic — a real inconsistency to resolve

VAT 15%, inclusive prices, back-out method (`net = price/1.15`, `vat = price − net`):

**Service PRINCIPAL** (buy 70 / sell 100) — *internally consistent*:
| | net | VAT |
|---|---|---|
| buy 70 | 60.87 | 9.13 |
| sell 100 | 86.96 | 13.04 |
- net-to-ZATCA = 13.04 − 9.13 = **3.91** (same whether from rounded or unrounded). ✓ matches spec
- margin = 86.96 − 60.87 = **26.09** (same both ways). ✓ matches spec

**Shipping PRINCIPAL** (buy 10 / sell 15) — *spec numbers require full-precision aggregation*:
| | net | VAT |
|---|---|---|
| buy 10 | 8.70 | 1.30 |
| sell 15 | 13.04 | 1.96 |
- net-to-ZATCA: **round-then-subtract** = 1.96 − 1.30 = **0.66**; **subtract-unrounded-then-round** = (1.956522 − 1.304348) = **0.65**. Spec says **0.65**.
- margin: **round-then-subtract** = 13.04 − 8.70 = **4.34**; **unrounded-then-round** = (13.043478 − 8.695652) = **4.35**. Spec says **4.35**.

**Implication:** the per-line figures we **post to the ledger** must be 2dp money (so journals balance and ZATCA line amounts are valid). If we post `VatOutput 1.96` and `VatInput 1.30`, the control accounts net to **0.66**, not the spec's 0.65. The spec's 0.65/4.35 only emerge from full-precision intermediates rounded once at the end — which a balanced 2dp ledger cannot post. **These two cannot both be authoritative.** This is the #1 decision in §11.

**✅ RESOLVED (CTO): round-per-line.** Phase-4 assertions therefore become — Shipping: net-to-ZATCA **0.66**, margin **4.34** (per-line buy 8.70/1.30, sell 13.04/1.96); Service: **3.91 / 26.09** (unchanged). This **supersedes the original spec's 0.65 / 4.35** for shipping, which were only reachable by full-precision-then-round and cannot be posted in a balanced 2dp ledger. `Money` rounds every line at 2dp `AwayFromZero`; control accounts (`VatOutput`/`VatInput`) sum these rounded amounts.

---

## 10. Immutable ZATCA invoice store (Phase 6 — shape proposed, fields to verify live)

Own context/schema, **append-only**: cleared invoices never updated/deleted; corrections only via **credit/debit notes** (new linked records). Proposed fields: `Uuid`, `Icv` (invoice counter), `Pih` (previous-invoice-hash chain), `InvoiceHash`, `CryptographicStamp`, `QrTlv`, `InvoiceType` (Standard B2B clearance / Simplified B2C reporting), `SignedXml`, `ClearedStatus`, `ArchiveRef`. Cleared XML archived via `IDocumentArchive` (in-memory until `Archive:Live`). Real submission behind `IZatcaClearanceClient`, flagged off.
**Phase-6 note:** I will verify this field set against the **current** official ZATCA/Fatoora Phase-2 technical specs at Phase 6 (they change) and flag any field I can't confirm. Not verifying now (out of Phase-0 scope).

---

## 11. Decisions needed before/within the build

### Assumptions I am making (correct me if wrong)
1. Currency is **SAR only**; `Money` enforces single-currency journals. No FX in scope.
2. The new engine **consumes** the existing Commission module's computed commission (on subtotal); it does not recompute commission math.
3. Standard rounding mode is `MidpointRounding.AwayFromZero` at 2dp (consistent with existing `FinanceMoney`/`CommissionMoney`) — pending §11.1.
4. DESIGN.md and all code go in the main worktree on `feat/partner-merchant-finance`; migrations are generated as files only, never applied.
5. "Merchant payout recorded as money owed" = a `MerchantPayable` **credit liability**, settled (disbursed) only when the flag is on.

### ACCOUNTANT / CTO decisions (blocking where noted)
1. **✅ RESOLVED (CTO) — Rounding policy = round-per-line.** Every line rounds at 2dp `AwayFromZero` and is posted; aggregates sum rounded lines. Shipping ⇒ 0.66 / 4.34; Service ⇒ 3.91 / 26.09. Supersedes spec's 0.65 / 4.35 (§9.1).
2. **✅ RESOLVED (CTO) — `VatTreatment` is configurable per partner type with per-partner overrides; no hardcoded default model.** Phase-4 tests set treatment explicitly (worked examples are PRINCIPAL). The real-world per-type default mapping (which of Aggregator/ThreePL/Carrier/Service/Marketplace are principal vs agent) is set in config by the accountant before go-live, not in code. *(Interpretation of "yes" — confirm if you meant a specific default mapping instead.)*
3. **✅ RESOLVED (CTO) — Invoicing.** Default = **Standard / B2B clearance**, Seller = platform, Buyer = partner/merchant with a **validated VAT number** (validation gates the Standard path). If a flow's buyer is a final consumer or non-VAT-registered entity ⇒ route to **Simplified (reporting)** and **flag to CTO** (do not force B2B).
4. **VAT legitimacy + negative-VAT handling.** Is the platform genuinely VAT-registered **principal** (buys then resells shipping/services with valid supplier tax invoices) so input VAT is reclaimable? And in a payout-dominant period, do we clamp invoice VAT at 0 and carry input-VAT credit on the return, or another treatment?
5. **Aggregator clearing reality.** Does the aggregator actually hold/disburse funds (making `AggregatorClearing`/`MerchantPayable` real cash liabilities), or is this informational until our own collection exists?
6. **Account chart sign-off.** Confirm the §4 account list/names and normal balances; add any GL accounts the accountant expects (e.g., separate VAT payable vs receivable, rounding-difference account).
7. **Standard vs Simplified selection rule.** Is it purely "buyer has a VAT number ⇒ Standard, else Simplified"? Confirm.

---

## 12. Webhook ingestion → settlement trigger (Addition C)

The inbound webhook is the **START** of the settlement state machine: it signals "collected / paid / delivered" from an aggregator or payment provider and moves a settlement **Collected → Allocated**. It **must not move money** — it only triggers the read-only allocation + journal computation. Reuses the **existing `Zahy.Webhooks`** module's HMAC primitive (`WebhookHmacSigner`) and append-only persistence patterns; inbound ingestion + routing are new. Real disbursement and live provider calls stay **flagged OFF**.

1. **Signature verification.** Every inbound settlement webhook is verified against the provider's signing secret (HMAC, reusing `WebhookHmacSigner`). Unverified or malformed payloads are **rejected and logged** — an unsigned settlement webhook is treated as hostile (never processed).
2. **Idempotency.** Dedupe by the provider's external transaction/event id, mapped onto the engine's idempotency key. Replaying the same event ⇒ **no second journal, no second state transition** (explicit test).
3. **Ack-then-process.** Respond **2xx immediately** on accept; allocation/journal computation runs **asynchronously**, so provider retries don't pile up or duplicate (retry-safe path tested). (Async worker stays in-process/flagged consistent with the host's disabled background workers.)
4. **Per-partner-type routing.** Route the event to the correct **flow profile** (Aggregator vs Service, §6.1) and that partner's **isolated books**, based on the payload's partner/type. **Unknown/unmapped partner ⇒ quarantine + flag, never auto-process.**
5. **Event log (dashboard source of truth).** Persist every received webhook **append-only**: raw payload, signature status, dedupe result, which settlement it triggered, and the resulting state transition. The dashboard reads this to show the full cycle per partner type.
6. **Explain link.** The event log row links webhook event → settlement → journal → state, so the explain endpoint (§13) can trace a transaction from "webhook arrived" to "reconciled."

**Worked end-to-end example (tests + short sample doc):** a mock aggregator sends a **signed "order collected, total 100"** webhook → engine **verifies** → **allocates** (merchant payout / delivery cost / platform commission / aggregator fee) → posts a **balanced journal** → state **Collected → Allocated** → row appears in the **event log** and is retrievable via **explain (§13)**. All mock/in-memory; real disbursement + live provider OFF.

---

## 13. Settlement explain / trace read API + dashboard contract (Addition B)

A **read-only** endpoint that, for any transaction or settlement, returns the full, verifiable breakdown of the cycle. Execution-free; behind the same flags. RBAC-gated and per-partner-type isolated.

Returns (stable DTO contract consumed by merchant/partner dashboards):
- **Collected total → allocation per party:** merchant payout, delivery cost, platform commission, shipping margin, aggregator fee.
- **VAT block:** VAT output / VAT input / **net-to-ZATCA**, **per line, with the rounding shown** (round-per-line, §9.1).
- **Journal:** the balanced debit/credit entries posted (debits == credits).
- **State:** Collected → Allocated → Invoiced → Cleared → Disbursed → Reconciled.
- **Context:** partner type + `VatTreatment` used (agent vs principal).
- **Trigger:** the linked webhook event(s) from §12.

**RBAC:** `Accountant` sees all books; `PartnerFinance` sees only own partner + own partner type (reusing the partner/tenant query-filter approach). DTO is versioned/stable so dashboards can show per-partner-type follow-up of the whole cycle. **Read-only/explain only — no execution, no disbursement.**

---

## 14. Phasing (REVISED — per CTO priority order; stop after each)

Headline priority requested: **Phase 1 (locked) → A (isolation) → C (webhook) → B (explain) → ZATCA.**

| Phase | Scope | Notes |
|---|---|---|
| **P1 ✅ LOCKED/DONE** | Money VO + double-entry primitives + balance invariant | Unchanged. 27 tests, suite green. |
| **P2 — A: Isolation (composed flow profiles)** | Settlement context + DbContext (migration generated as files only) + `ISettlementFlowProfile` strategy (Aggregator, Service) + per-type isolated account trees + own `SettlementCase` state machine + idempotency + **RBAC** | Absorbs the original "settlement context + state machine + idempotency" and "per-partner-type isolation + RBAC" phases. Composition, not inheritance (§6.1). |
| **P3 — Allocation engine** ⚠️ | Cost/markup VO (service/shipping/item) + configurable VAT (agent/principal) + negative-VAT guard + **exact worked-number tests** (shipping 0.66/4.34; service 3.91/26.09) | **Prerequisite for C and B** — their worked examples require real allocation + VAT numbers. See sequencing note below. |
| **P4 — C: Webhook ingestion → trigger** | §12: signature verify, idempotency, ack-then-process, per-type routing, append-only event log, explain link; flagged off | Drives Collected→Allocated; moves no money. |
| **P5 — B: Explain/trace read API + dashboard DTO** | §13: full breakdown, RBAC-gated, per-type isolated, read-only | |
| **P6 — ZATCA** | Immutable invoice store + integration boundary (flagged off); verify field set vs **current** live ZATCA/Fatoora Phase-2 specs | Last, as requested. |

> ⚠️ **Sequencing note (needs your nod).** Your literal order is A → C → B → ZATCA, with no allocation/VAT step listed. But C's worked example ("allocate collected 100 → splits → balanced journal") and B's VAT-breakdown both **depend on** the cost/markup + VAT engine. I've inserted it as **P3 (Allocation engine)** between A and C. Confirm you want it as its own phase (recommended, for clean exact-number test isolation) **or** folded into the front of the C phase. Everything else follows your order exactly. ZATCA stays last.
