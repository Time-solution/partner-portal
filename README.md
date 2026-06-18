# Zahy Partner Platform

Physically separate **Partner Platform** for the Zahy multi-tenant SaaS commerce platform (KSA market: ZATCA e-invoicing, SAR, Arabic RTL + English, data hosted inside Saudi Arabia).

## Stack

- **Backend:** .NET / ABP Framework — **API-only** (no built-in UI), modular monolith, single SQL Server database with a DbContext/schema per module.
- **Frontend:** **React** (Vite + TypeScript + Tailwind + shadcn/ui), consuming the ABP REST APIs and authenticating via OpenIddict (OIDC).
- **Identity:** owned Identity layer = ABP Identity + OpenIddict (Authorization Code + PKCE for users, Client-Credentials + scopes for partner systems).

> Core Zahy keeps its existing **Angular** frontend and is **not** rewritten here. The Web POS lives in core Zahy; this platform integrates with it.

## Ownership guardrail (read before contributing)

- **Owned source (safe to build on):** Catalog, Theming, CmsKit, EInvoicing.ZatcaFatoora, Payments.
- **Compiled third-party packages (makookapp) — DO NOT modify, fork, or decompile:** Commerce, Promotions, Shipping.Oto, Tajer.Abp.OpenIddict, Tajer.Legacy.
- All interaction with compiled packages is **outside-in**: public services, owned contracts/interfaces (Anti-Corruption Layer), read-only data reads (CDC), or webhooks. Never reach into their internals or write to Commerce tables directly.

Full engineering rules live in [`.cursorrules`](./.cursorrules).

## Planned modules (`Zahy.*`)

| Module | Responsibility |
| --- | --- |
| `Zahy.Identity` | Owned IdP — ABP Identity + OpenIddict (OIDC), MFA-ready |
| `Zahy.PartnerPlatform` | Host-level (cross-tenant) partner aggregates & connectors |
| `Zahy.Webhooks` | Signed (HMAC) webhook integration — outbox + retries + dead-letter + logs (NOT an internal event bus) |
| `Zahy.OrderLedger` | Order capture via read-only SQL Server CDC → idempotent ledger |
| `Zahy.Commission` | Commission / ledger / payouts — append-only & idempotent |

Each module follows ABP layering: `Domain.Shared`, `Domain`, `Application.Contracts`, `Application`, `EntityFrameworkCore`, `HttpApi`.

## Key architectural rules

- **Webhooks, not an internal event bus**, for integration between components and partners.
- **Tenant isolation** enforced on every data access.
- **Money code is append-only and idempotent** — never mutate ledger rows in place.
- **No secrets in code or commits** — use the secret store / environment config.

## Repository layout (Phase 0 scaffold)

```
partner-portal/
├─ Zahy.PartnerPlatform.slnx
├─ src/
│  ├─ Directory.Build.props      # net10.0 + shared build settings
│  ├─ Directory.Packages.props   # central ABP package versions (10.4.1)
│  ├─ Zahy.Identity/
│  ├─ Zahy.PartnerPlatform/
│  ├─ Zahy.Webhooks/
│  ├─ Zahy.OrderLedger/
│  ├─ Zahy.Commission/
│  └─ Zahy.HttpApi.Host/        # API-only host
└─ frontend/
   └─ partner-portal-web/       # React + Vite + TS + Tailwind + shadcn/ui
```

## Design documents (source of truth)

> Replace these placeholders with the real links.

- BRD — _TODO_
- Architecture — _TODO_
- SRS — _TODO_
- SDD — _TODO_

## Status

Phase 0 — scaffold in progress (empty, compiling skeletons; no business logic yet).
