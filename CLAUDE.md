# Bodega Abierta — Core API (.NET)

Project rules for every AI coding session (Claude Code or similar) in this repository. Human
contributors: see `CONTRIBUTING.md` and `docs/arquitectura.md`.

## Context
Bodega Abierta is an open-source (Apache-2.0) warehouse management system for Mexican food banks, a
project of RapiJazz (a civil association in formation). This repo is the **C# / ASP.NET Core API**
(ADR 0001). The offline-first PWA (Next.js) is a separate repository that consumes this API and shares
no code with it.

## Non-negotiable rules
- Never commit secrets, connection strings or real personal data. Use `user-secrets` and `.env`.
- Never claim in code, seed data, docs or UI text that RapiJazz is a *donataria autorizada*, issues
  tax-deductible receipts, or holds any official certification or folio. It does not yet.
- Every tenant-owned entity implements `ITenantOwned`. Do not use `IgnoreQueryFilters` without a
  comment explaining why. Query filters protect reads only; writes need the SaveChanges guard.
- Personal data (names, phones, emails, addresses of beneficiaries and donors) is protected under
  Mexico's LFPDPPP (2025). Collect the minimum and keep it out of logs and audit snapshots.
- No logic in controllers. Domain rules live in `Domain`, use cases in `Application`.
- Async + `CancellationToken` everywhere.
- Business errors are `DomainRuleException` with a stable `code` (e.g. `lot.expiry_required`); the PWA
  depends on these codes, so never rename an existing one.
- Every behavior change ships with tests. Schema changes ship with their EF Core migration.
- `TODO(name):` comments are tasks reserved for a person to write by hand. Do not implement them unless
  that person explicitly asks you to; you may review their code.

## Stack
- .NET 10 (LTS), ASP.NET Core controllers, `Asp.Versioning.Mvc` (`/api/v1/...`), OpenAPI
- EF Core + SQL Server (Docker, host port 1434), migrations in `Infrastructure/Persistence/Migrations`
- FluentValidation, RFC 9457 ProblemDetails
- xUnit v3 + NSubstitute + FluentAssertions, Testcontainers.MsSql
- Health checks (`/health`), GitHub Actions CI; Serilog and OpenTelemetry planned in Phase 6

## Solution layout
```
src/BodegaAbierta.Api              controllers, auth, tenancy, HTTP concerns
src/BodegaAbierta.Application      services per use case, contracts, validators, rules engine
src/BodegaAbierta.Domain           entities and business rules, no EF/ASP.NET references
src/BodegaAbierta.Infrastructure   DbContext, configurations, migrations, audit interceptor
tests/BodegaAbierta.UnitTests
tests/BodegaAbierta.IntegrationTests
docs/adr/, docs/arquitectura.md
```

## Domain
- **FoodBank** (tenant), **StorageLocation**, **Donor**, **Product** (category, unit, perishable flag)
- **DonationIntake**: donor, lines → one **InventoryLot** per line (quantity, expiry, location, status)
- **Beneficiary** (organization or household), **Distribution** (FEFO-allocated lots, beneficiary, pickup slot)
- **PickupSlot**: date/time window with capacity
- **PolicySet** / **PolicyRule**: versioned rules per food bank
- **AuditEntry**: written automatically by `AuditSaveChangesInterceptor` (before/after JSON, actor, timestamp)

## Rules engine
PolicySets move Draft → PendingApproval → Active → Retired, approved by a `Coordinator` who did not
submit them; at most one Active per food bank. Each rule type is an `IPolicyRuleEvaluator` strategy:
`FefoAllocation`, `ExpiryAlert`, `PickupWindow`, `BeneficiaryLimit`, `DonorDropOffHours`.
`POST /api/v1/policies/evaluate` is a dry run returning `{ allowed, violations[], allocation[] }`.

## Pending decisions
- **Inventory ledger (ADR 0002, Proposed):** replace mutable `QuantityOnHand` with append-only
  `StockMovement` rows in integer grams, with client-generated ids for offline sync. Until it is
  accepted, do not build new features on top of `QuantityOnHand`.
- **Database:** possible move from SQL Server to PostgreSQL (Npgsql) to cut hosting costs. Not decided.

## Reporting (Phase 6)
`GET /api/v1/reports/impact?from=&to=` → kg rescued, waste %, CO₂e estimate (configurable factor), as
one aggregate SQL query; document the plan in `docs/query-tuning.md`.

## Phases
1–4 done (skeleton, inventory intake, policies + audit, rules engine + distributions).
5. Tests: unit per rule, integration incl. tenant isolation on reads and writes.
6. Impact report, observability, runbook.

## Conventions
One commit per logical change, conventional messages (`feat:`, `fix:`, `docs:`), work on a branch and
open a pull request; `main` only changes through reviewed PRs with green CI.

## Working environment
- Work happens in **Claude Code on the web (cloud sessions)**, not on a local machine.
- **One branch and one pull request per task.** Do not mix unrelated changes in the same PR.
- The cloud environment has **no Docker**, so the integration tests (`tests/BodegaAbierta.IntegrationTests`,
  which use Testcontainers.MsSql) run **only in GitHub Actions**. Locally run only the unit tests:
  `dotnet test tests/BodegaAbierta.UnitTests`. Check the PR's CI for integration results.
- If the .NET 10 SDK is missing, install it with `apt-get install -y dotnet-sdk-10.0` (the
  `dot.net` install script is blocked by the network policy); `global.json` rolls forward to it.
