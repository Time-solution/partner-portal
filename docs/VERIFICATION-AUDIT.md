# Zahy Partner Platform — Verification Audit

**Date:** 2026-06-19
**Branch under test:** `feat/partner-merchant-finance` (commit `168341c`) **plus uncommitted working-tree WIP** in the main worktree. This is the cumulative branch — Identity (Phase 1) → Partner Onboarding (Phase 2) → Commission (Phase 5) → Finance (Steps 2–7) are all merged in.
**Scope:** Read-and-run verification pass across Phases 1–5 + Finance. No features added. Genuine bugs fixed; design/security questions flagged, not silently changed.

> ⚠️ **Reviewer note:** The Commission and Finance modules are pending CTO/finance review. Every money/PII concern below is **flagged, never loosened**. One stated guarantee (Marketplace `catalog:write`) is intentionally **not** met by the code and is escalated rather than "made green" — see Discrepancies.

---

## How "Status" is defined

- **GREEN** — a test exists whose body actually asserts the guarantee (verified by reading the assertions, not by name match).
- **MISSING** — no such test exists.
- **ADDED** — a test was missing and was written + made to pass during this audit.
- **DISCREPANCY** — a test/impl exists but the *production behavior contradicts the stated guarantee*; escalated for owner decision (not auto-fixed).

> **Suite-run reconciliation — DONE:** The full `dotnet test` run completed: **203 passed, 0 failed, 0 skipped** (190 backend + 13 frontend). Every GREEN below is backed by a passing test. Counts per project in *Task 1 — Suite result*.

---

## Identity

| Guarantee | Test name (file:line) | Status |
|---|---|---|
| Login end-to-end | `Should_Login_Seeded_Admin_And_Record_Success_Audit` ([AccountAppServiceTests.cs:40](../test/Zahy.Identity.Tests/Account/AccountAppServiceTests.cs#L40)) | GREEN |
| Scope handling | `Should_Provision_Confidential_M2M_Client_With_Type_Scopes` ([PartnerM2MClientProvisionerTests.cs:26](../test/Zahy.Identity.Tests/OpenIddict/PartnerM2MClientProvisionerTests.cs#L26)) | GREEN |
| Tenant boundary | `Should_Seed_Merchant_Roles_Per_Tenant_And_Isolate_Grants` ([ZahyRoleSeedTests.cs:80](../test/Zahy.Identity.Tests/Roles/ZahyRoleSeedTests.cs#L80)) | GREEN |
| Cross-boundary role-assignment denial | `MerchantOwner_Cannot_Cross_Tenant` + `PartnerOwner_Cannot_Cross_Partner` ([ZahyRoleAssignmentPolicyTests.cs:68](../test/Zahy.Identity.Tests/Roles/ZahyRoleAssignmentPolicyTests.cs#L68), [:95](../test/Zahy.Identity.Tests/Roles/ZahyRoleAssignmentPolicyTests.cs#L95)) | GREEN |

## Partner Onboarding

| Guarantee | Test name (file:line) | Status |
|---|---|---|
| Lifecycle illegal transitions | `Should_Reject_Illegal_Transitions` ([PartnerLifecyclePolicyTests.cs:35](../test/Zahy.PartnerPlatform.Tests/Partners/PartnerLifecyclePolicyTests.cs#L35)) | GREEN |
| Approve blocked without BankInfo | `Should_Reject_Approve_When_BankInfo_Incomplete` ([PartnerAdminAppServiceTests.cs:116](../test/Zahy.PartnerPlatform.Tests/Partners/PartnerAdminAppServiceTests.cs#L116)) | GREEN |
| Atomic rollback on approve | `Should_Rollback_Approve_When_M2M_Provision_Fails` ([PartnerApproveRollbackTests.cs:49](../test/Zahy.PartnerPlatform.Tests/Partners/PartnerApproveRollbackTests.cs#L49)) | GREEN |
| M2M secret returned once + not logged | `Should_Approve_Pending_Partner_And_Return_One_Time_M2M_Secret` + `Should_Not_Log_M2M_Client_Secret_In_Approve_Audit` ([PartnerAdminAppServiceTests.cs:33](../test/Zahy.PartnerPlatform.Tests/Partners/PartnerAdminAppServiceTests.cs#L33), [:77](../test/Zahy.PartnerPlatform.Tests/Partners/PartnerAdminAppServiceTests.cs#L77)) | GREEN |
| **Marketplace scope includes `catalog:write`** | `Should_Map_Conservative_Default_Scopes` ([PartnerTypeScopePolicyTests.cs:15](../test/Zahy.PartnerPlatform.Tests/Partners/PartnerTypeScopePolicyTests.cs#L15)) | **DISCREPANCY** |
| partner-team direct-id → 403 | `Should_Return_403_When_Partner_User_Accesses_Other_Partners_Team_Member` ([PartnerTeamIsolationTests.cs:32](../test/Zahy.PartnerPlatform.Tests/Partners/PartnerTeamIsolationTests.cs#L32)) | GREEN |

## Webhooks / Ledger

| Guarantee | Test name (file:line) | Status |
|---|---|---|
| HMAC sign + tamper | `Should_Sign_And_Verify_Payload_With_Known_Secret` + `Should_Reject_Tampered_Payload` ([WebhookHmacSignerTests.cs:9](../test/Zahy.Webhooks.Tests/WebhookHmacSignerTests.cs#L9), [:22](../test/Zahy.Webhooks.Tests/WebhookHmacSignerTests.cs#L22)) | GREEN |
| Outbox idempotency | `Should_Return_Same_Outbox_Id_For_Duplicate_Idempotency_Key` ([WebhookOutboxIdempotencyTests.cs:22](../test/Zahy.Webhooks.Tests/WebhookOutboxIdempotencyTests.cs#L22)) | GREEN |
| Retry → DLQ | `Should_Move_Message_To_Dead_Letter_After_Max_Retries` ([WebhookRetryDeadLetterTests.cs:32](../test/Zahy.Webhooks.Tests/WebhookRetryDeadLetterTests.cs#L32)) | GREEN |
| Subscription isolation 403 | `Should_Return_403_When_Partner_Accesses_Other_Partners_Subscription` ([WebhookSubscriptionIsolationTests.cs:33](../test/Zahy.Webhooks.Tests/WebhookSubscriptionIsolationTests.cs#L33)) | GREEN |
| Ledger ingest idempotency | `Should_Create_Single_Ledger_Entry_For_Duplicate_Source_Id_And_Version` ([OrderLedgerIngestionIdempotencyTests.cs:22](../test/Zahy.OrderLedger.Tests/OrderLedgerIngestionIdempotencyTests.cs#L22)) | GREEN |
| Append-only | `Should_Append_New_Version_Instead_Of_Updating_Existing_Row` ([OrderLedgerAppendOnlyTests.cs:22](../test/Zahy.OrderLedger.Tests/OrderLedgerAppendOnlyTests.cs#L22)) | GREEN |
| Duplicate-ingest → no webhook refire | `Duplicate_Ingest_Does_Not_Refire_Webhook` ([OrderLedgerWebhookFlowTests.cs:55](../test/Zahy.OrderLedger.Tests/OrderLedgerWebhookFlowTests.cs#L55)) | GREEN |

## Connectors

| Guarantee | Test name (file:line) | Status |
|---|---|---|
| Registry / capability gating | `Should_Resolve_Connector_By_Explicit_Code` ([ConnectorRegistryTests.cs:32](../test/Zahy.Connectors.Tests/Connectors/ConnectorRegistryTests.cs#L32)) | GREEN |
| Canonical ↔ snapshot round-trip | `Should_Round_Trip_Canonical_Order_Through_Ledger_Snapshot` ([CanonicalOrderMapperTests.cs:17](../test/Zahy.Connectors.Tests/Mapping/CanonicalOrderMapperTests.cs#L17)) | GREEN |
| Status / payment independence | `Should_Map_InTransit_With_Unpaid_Payment_To_Non_Paid_Ledger_Record` ([CanonicalOrderMapperTests.cs:86](../test/Zahy.Connectors.Tests/Mapping/CanonicalOrderMapperTests.cs#L86)) | GREEN |
| SLA expiry blocks accept | `Should_Return_Expired_When_Accepting_Past_Accept_Deadline` ([MockAggregatorConnectorTests.cs:100](../test/Zahy.Connectors.Tests/Aggregators/MockAggregatorConnectorTests.cs#L100)) | GREEN |
| Status-update-as-new-version (not duplicate order) | `Should_Append_ThreePL_Status_Update_As_New_SourceVersion_Not_Duplicate_Order` ([ConnectorOrderStatusIngestionTests.cs:29](../test/Zahy.Connectors.Tests/Ingestion/ConnectorOrderStatusIngestionTests.cs#L29)) | GREEN |
| Full-chain duplicate → 0 ledger + 0 outbox | `Duplicate_Aggregator_Receive_Does_Not_Create_Duplicate_Ledger_Or_Outbox_Entry` ([ConnectorOrderWebhookNoRefireTests.cs:51](../test/Zahy.Connectors.Tests/Ingestion/ConnectorOrderWebhookNoRefireTests.cs#L51)) | GREEN |

## Commission

| Guarantee | Test name (file:line) | Status |
|---|---|---|
| Exact-decimal rounding | `Should_Round_Commission_Away_From_Zero` ([CommissionCalculatorTests.cs:131](../test/Zahy.Commission.Tests/Rules/CommissionCalculatorTests.cs#L131)) | GREEN |
| Duplicate source+rule → one accrual | `Duplicate_Source_And_Rule_Creates_One_Accrual` ([CommissionLedgerAppendOnlyTests.cs:24](../test/Zahy.Commission.Tests/Ledger/CommissionLedgerAppendOnlyTests.cs#L24)) | GREEN |
| Reversal-as-new-row | `Reversal_Creates_Offsetting_Row_Original_Unchanged` ([CommissionLedgerAppendOnlyTests.cs:56](../test/Zahy.Commission.Tests/Ledger/CommissionLedgerAppendOnlyTests.cs#L56)) | GREEN |
| Subtotal basis (not total) | `Should_Compute_On_Subtotal_Not_Total` ([CommissionCalculatorTests.cs:155](../test/Zahy.Commission.Tests/Rules/CommissionCalculatorTests.cs#L155)) | GREEN |
| Winner-per-fee-type | `Overlapping_Rules_Do_Not_Double_Accrue_Same_Fee` ([CommissionCalculatorTests.cs:241](../test/Zahy.Commission.Tests/Rules/CommissionCalculatorTests.cs#L241)) | GREEN |
| Unpaid does not accrue | `Unpaid_Order_Ingest_Does_Not_Accrue` ([CommissionOrderAccrualFlowTests.cs:95](../test/Zahy.Commission.Tests/Accrual/CommissionOrderAccrualFlowTests.cs#L95)) | GREEN |

## Finance

| Guarantee | Test name (file:line) | Status |
|---|---|---|
| Account opens only on KYC Verified | `Account_Does_Not_Open_Until_Verified` ([FinanceKycStep3Tests.cs:67](../test/Zahy.Finance.Tests/Kyc/FinanceKycStep3Tests.cs#L67)) | GREEN |
| Balance == sum of postings | `Balance_Equals_Sum_Of_Postings_Exactly` ([FinanceAccountStep2Tests.cs:124](../test/Zahy.Finance.Tests/Accounts/FinanceAccountStep2Tests.cs#L124)) | GREEN |
| Postings append-only | `Postings_Are_Append_Only` ([FinanceAccountStep2Tests.cs:92](../test/Zahy.Finance.Tests/Accounts/FinanceAccountStep2Tests.cs#L92)) | GREEN |
| Documents use verified KYC not raw | `Documents_Use_Verified_Kyc_Not_Raw_Submission` ([FinanceKycStep3Tests.cs:104](../test/Zahy.Finance.Tests/Kyc/FinanceKycStep3Tests.cs#L104)) | GREEN |
| No KYC/PII in logs | `Kyc_Sensitive_Fields_Are_Never_Written_To_Logs` ([FinanceKycStep3Tests.cs:213](../test/Zahy.Finance.Tests/Kyc/FinanceKycStep3Tests.cs#L213)) | GREEN |
| Gapless + idempotent invoice numbering | `Invoice_Idempotent_Retry_Returns_Same_Number` ([FinanceInvoiceStep5Tests.cs:244](../test/Zahy.Finance.Tests/Invoicing/FinanceInvoiceStep5Tests.cs#L244)) | GREEN |
| Concurrent invoice generation gapless+unique | `Concurrent_Invoice_Generations_Are_Gapless_And_Unique` ([FinanceInvoiceNumberConcurrencyTests.cs:22](../test/Zahy.Finance.Tests/SqlServer/FinanceInvoiceNumberConcurrencyTests.cs#L22)) — *SQL Server-backed* | GREEN |
| Download endpoint re-checks ownership (403 on guessed id) | `Download_Endpoint_Rechecks_Ownership` ([FinancePortalStep6Tests.cs:122](../test/Zahy.Finance.Tests/Portal/FinancePortalStep6Tests.cs#L122)) | GREEN |
| Portal views read-only | `Portal_Account_Views_Are_Read_Only` ([FinancePortalStep6Tests.cs:141](../test/Zahy.Finance.Tests/Portal/FinancePortalStep6Tests.cs#L141)) | GREEN |

---

## Discrepancies & flags (do NOT auto-fix — owner decision required)

### D1 — Marketplace scope does NOT include `catalog:write` (security; Architect decision)
- **Stated guarantee:** "Marketplace scope includes `catalog:write`."
- **Actual code:** [PartnerTypeScopePolicy.cs:16](../src/Zahy.PartnerPlatform/Zahy.PartnerPlatform.Domain.Shared/Partners/PartnerTypeScopePolicy.cs#L16) maps `Marketplace → [OrdersRead, CatalogRead]`. The class is explicitly documented *"Conservative default OAuth scopes per partner connector type (PON-4)"*, and [PartnerTypeScopePolicyTests.cs:14](../test/Zahy.PartnerPlatform.Tests/Partners/PartnerTypeScopePolicyTests.cs#L14) asserts exactly `[OrdersRead, CatalogRead]`.
- **Why not fixed:** Granting `catalog:write` **broadens a partner's OAuth permissions** — a security boundary change. Per `.cursorrules §3/§8`, identity/security changes are Architect-reviewed. "Making the test green" here would be a deliberate privilege escalation against an intentional conservative-defaults decision.
- **Decision needed:** Is the SRS requirement that Marketplace partners receive `catalog:write` by default (then PON-4 conservative-defaults policy + its test must change, under Architect sign-off), **or** is `CatalogRead` correct and the guarantee statement stale? Until resolved, marked DISCREPANCY, not MISSING/ADDED.

### D2 — Host base connection string diverges from DbMigrator (latent config footgun)
- `src/Zahy.DbMigrator/appsettings.json` → `localhost\SQLEXPRESS01; Database=Zahy`
- `src/Zahy.HttpApi.Host/appsettings.Development.json` → `localhost\SQLEXPRESS01; Database=Zahy` ✅ **matches in Development (the default local env)**
- `src/Zahy.HttpApi.Host/appsettings.json` (base) → `(LocalDb)\MSSQLLocalDB; Database=ZahyPartnerPlatform` ❌ **differs**
- **Impact:** In the normal `dotnet run` (Development) flow they align, so login works. But running the host in any non-Development environment silently targets a *different, unseeded* database → login fails with confusing "bad credentials". Recommend aligning the base or documenting that the host must run in Development locally. (`DbMigrator/appsettings.json` is part of the current uncommitted WIP — appears to have just been aligned.)

### D3 — Dependency advisories NU1903 (one shipping, one test-only)
- **`System.Security.Cryptography.Xml 9.0.0`** — high-severity ([GHSA-w3x6-4m5h-cxqf](https://github.com/advisories/GHSA-w3x6-4m5h-cxqf), [GHSA-37gx-xxp4-5rgx](https://github.com/advisories/GHSA-37gx-xxp4-5rgx)). Pulled transitively into the **EF Core / OpenIddict** projects that the **host ships** — not test-only. Recommend bumping the transitive pin in `Directory.Packages.props`. Medium priority (cert/XML-signing path).
- **`SQLitePCLRaw.lib.e_sqlite3 2.1.11`** — high-severity ([GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)). Used only by the in-memory SQLite **test** harness (not shipped). Low runtime risk.

### D4 — OIDC redirect URI vs actual frontend port (environment, not a code bug)
- Partner SPA client `zahy-partner-web` is seeded with redirect `http://localhost:5173/auth/callback`; CORS allows `5173,5174`. This matches the **intended/default Vite port 5173**.
- **In this machine's environment, ports 5173/5174/5175 are already held by other `node` dev servers**, so Vite fell back to **5176**. At 5176 the SPA login would fail with `invalid redirect_uri` (and CORS would block API calls).
- **Not fixed in code** — the registered config is correct for a clean environment. The genuine resolution is to run the frontend on **5173** (free the port) rather than chase an ephemeral port by registering 5176. Flagged for the user to decide (free 5173 vs. register 5176 for a live browser-login demo).

---

## Task 1 — Suite result

`dotnet test Zahy.PartnerPlatform.slnx` + frontend `vitest run`. **Total: 203 passed / 0 failed / 0 skipped. No flaky tests observed.**

| Project | Passed | Failed | Skipped |
|---|---|---|---|
| Zahy.PartnerPlatform.Tests | 50 | 0 | 0 |
| Zahy.Identity.Tests | 35 | 0 | 0 |
| Zahy.Finance.Tests | 31 | 0 | 0 |
| Zahy.Commission.Tests | 29 | 0 | 0 |
| Zahy.Connectors.Tests | 27 | 0 | 0 |
| Zahy.OrderLedger.Tests | 6 | 0 | 0 |
| Zahy.Webhooks.Tests | 6 | 0 | 0 |
| Zahy.PlatformIntegration.Tests | 6 | 0 | 0 |
| **Backend subtotal** | **190** | **0** | **0** |
| Frontend (vitest: permissions, session, nav) | 13 | 0 | 0 |
| **Grand total** | **203** | **0** | **0** |

The SQL Server-backed `FinanceInvoiceNumberConcurrencyTests` passed, confirming the concurrency/gapless guarantee against real SQL Server (not just SQLite). **OneDrive note:** the only material slowdown was NuGet restore over a throttled network (~6 min/project, many 100s timeouts); packages cache after first success.

---

## Task 3 — Accounting verdict (dev partner `2222…2001`)

**Money is correct. No ledger/data bug. No display bug found.**

| Check | Result |
|---|---|
| Postings (raw SQL `FinAccountPostings`) | **1 row**: `billing.charge`, **+49.00 SAR**, PostedAt 2026-06-19 06:14:40 |
| `SUM(PostingAmount)` (SQL) | **49.00** |
| Balance via API `GET /api/partner/account` | **49.00 SAR**, `kycStatus=Verified`, `isOperational=true`, accountId matches DB row |
| Postings via API `/postings` | 1 item, `postingAmount=49.00`, `runningBalance=49.00` — matches SQL |
| CSV export `/export?format=Csv` | row + **Total 49.00 SAR** ✓ |
| XLSX export `/export?format=Xlsx` | valid OpenXML (PK magic, 6532 bytes, correct MIME) ✓ |
| PDF | **No document seeded** (`/documents` → `[]`). Dev seed creates a posting, not a generated invoice; partner portal is intentionally read-only so cannot self-generate. PDF/invoice generation covered by passing unit tests (`FinanceDocumentStep4Tests`, `FinanceInvoiceStep5Tests`). Not reproduced live. |
| Sign convention | `billing.charge` → **+49** (PlatformEarns positive), per `FinancePostingSignMapper.MapBillingChargeAmount`. ✓ |
| Balance storage | `FinPartnerAccounts` has **no Balance column** — balance is *computed* from postings, so it cannot drift from the ledger (append-only by construction). ✓ |
| Commission basis (Subtotal not Total) | **N/A in live dev data** — seed has no commission accrual postings (only a billing charge). Guarantee covered by passing unit test `Should_Compute_On_Subtotal_Not_Total`. |
| Arabic/RTL rendering | **Display code correct, no bug.** `AccountBillingPage.tsx` uses `Intl.NumberFormat(ar-SA/en-SA, {style:currency})` and `Intl.DateTimeFormat`; root `dir=rtl` for Arabic. The numeric value (49.00) is unchanged across locales — Arabic only changes digit glyphs/symbol. **Pixel-level RTL render not visually confirmed** (SPA login blocked by the port issue in D4); formatting logic verified by reading. |

**Verdict:** the **49.00 SAR** figure is decimal-exact and identical across SQL ledger → API → CSV → XLSX. Sign convention and append-only/computed-balance design are correct. The only items not reproduced *live* are PDF and commission-subtotal basis — both because the dev seed doesn't create that data, and both are covered by passing tests. No money bug; no display bug.

---

## Task 4 — Migration, DB, and login

| Item | Result |
|---|---|
| Host vs DbMigrator connection string | **Match in Development** (both `localhost\SQLEXPRESS01; Database=Zahy`). Base `appsettings.json` differs — see **D2** (latent footgun). |
| DbMigrator run | Clean: all 7 module migrators ran, `IDataSeeder.SeedAsync()` completed (`DOTNET_ENVIRONMENT=Development`, `DevSeed.Enabled=true`). |
| Migrations applied | All 9 present in `__EFMigrationsHistory` (Identity, PartnerPlatform, Webhooks, OrderLedger, Connectors, Commission + AddOrderMonetarySplit, Finance + AddBillingChargeTarget). **None pending.** Single DB / modular monolith per `.cursorrules §4`. |
| Dev seed present | `AbpUsers`: **admin, partner, merchant** (+ superadmin). Partner has `PartnerId` claim `2222…2001`. Partner **finance account opened** (KYC Verified) with the 49 SAR posting. Merchant finance accounts: 0 (partner-only seed, expected). |
| Login (live host) | `POST /api/account/login` — **partner / 1q2w3E* → `success:true`**; admin → `success:true`; wrong password → `InvalidCredentials` (negative control). |
| URLs | **API:** https://localhost:44300 (http 44301) · **Swagger:** https://localhost:44300/swagger · **OIDC discovery:** /.well-known/openid-configuration (200) · **Frontend (intended):** http://localhost:5173 · **Frontend (this run):** http://localhost:5176 (see D4). |
| OIDC redirect match | See **D4** — registered 5173 matches the intended port; environment forced Vite to 5176 (mismatch is environmental, not code). |
| "Reaches Account & Billing" | Verified at the data/API layer (`/api/partner/account` returns the account for the logged-in partner). Full SPA click-through not performed due to D4 port/login blocker. |

> A host instance was already running (PID 34420) on 44300 bound to the seeded `Zahy` DB; verification used it. The migrator and SQL queries used the same DB.

---

## Environment notes
- **Tooling:** .NET SDK 10.0.300, Node 24.16, npm 11.13. SQL Server `SQLEXPRESS01` running (also a default `SQLEXPRESS` instance).
- **Repo path is under OneDrive** (`…\OneDrive\سطح المكتب\…`). The dominant slowdown in this pass was **NuGet restore over a throttled network** (~6 min/project, many 100s HTTP timeouts), not disk. Moving the repo to a local path (e.g. `C:\dev`) and/or warming the NuGet cache would remove most of the wall-clock cost on re-runs (packages cache after first success).
- **Working location:** verification ran in the main worktree on `feat/partner-merchant-finance` **including uncommitted WIP**, because that is the genuine current state of the code. Nothing was committed or reverted.

*(Suite PASS/FAIL counts and the accounting verdict are appended below once the live run and DB queries complete.)*
