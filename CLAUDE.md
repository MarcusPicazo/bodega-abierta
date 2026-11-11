# Bodega Abierta — Core API (.NET)

## Context
Bodega Abierta is an open-source (Apache-2.0) warehouse management system for Mexican food banks,
run by RapiJazz A.C. The repo (github.com/MarcusPicazo/bodega-abierta) is currently empty.
Decision: the backend is a **C# / ASP.NET Core API**; the offline-first PWA (Next.js) comes later and
consumes this API. Record this decision in `docs/adr/0001-dotnet-backend.md`.

## Owner learning goal (important)
The owner is preparing for a C#/.NET interview. After each phase:
1. Explain in Spanish what was built, why, and which .NET concept it demonstrates.
2. Leave 1–2 small `// TODO(Marcus):` tasks for him to implement by hand, then review his code.
3. Never commit secrets.

## Stack
Same as the CIT-Sync Rules API so patterns are reused:
- .NET LTS, ASP.NET Core controllers, `Asp.Versioning.Mvc`, OpenAPI
- EF Core + SQL Server (Docker), migrations
- FluentValidation, ProblemDetails
- xUnit + NSubstitute + FluentAssertions, Testcontainers.MsSql
- Serilog, OpenTelemetry, health checks, GitHub Actions CI

## Solution layout
```
src/BodegaAbierta.Api
src/BodegaAbierta.Application
src/BodegaAbierta.Domain
src/BodegaAbierta.Infrastructure
tests/BodegaAbierta.UnitTests
tests/BodegaAbierta.IntegrationTests
docs/adr/, docs/runbook.md, docs/query-tuning.md
```

## Multi-tenancy
Every row belongs to a `FoodBank` (tenant). Tenant resolved from header/claim; EF Core global query
filter on `FoodBankId`. Integration test proves tenant A cannot read tenant B.

## Domain
- **Donor**, **Product** (category, unit, perishable flag)
- **InventoryLot**: product, quantity (kg / units), expiry date, location, status
- **DonationIntake**: donor, lots received, received by, timestamp
- **Beneficiary** (organization or household), **Distribution**: lots allocated, beneficiary, pickup slot
- **PickupSlot**: date/time window, capacity
- **AuditEntry**: automatic via SaveChanges interceptor (before/after JSON, actor, timestamp)

## Rules engine (the "schedule manager" part)
Versioned **PolicySets** per food bank, states Draft → PendingApproval → Active → Retired
(approved by a coordinator). Rule types:
- `FefoAllocation` — always allocate the lot that expires first
- `ExpiryAlert` — flag lots expiring within N days
- `PickupWindow` — allowed days/hours and slot capacity
- `BeneficiaryLimit` — max kg per beneficiary per period
- `DonorDropOffHours` — when donors can deliver
`POST /api/v1/policies/evaluate` returns `{ allowed, violations[] }` for a proposed distribution or pickup.

## Reporting
`GET /api/v1/reports/impact?from=&to=` → kg rescued, waste %, CO₂e estimate (configurable factor),
implemented as an efficient aggregate SQL query (document plan in query-tuning.md).

## Phases
1. Skeleton, Docker SQL Server, DbContext with tenant filter, first migration, CI.
2. Donors, products, intake → inventory lots (with validation + concurrency).
3. Policy sets: versioning, approval state machine, audit.
4. Rules engine + FEFO allocation for distributions + pickup slots.
5. Tests (unit per rule, integration incl. tenant isolation).
6. Impact report, observability, runbook, ADRs, README (EN/ES) with AI-usage section.

## Conventions
Async + CancellationToken everywhere, no logic in controllers, commit per phase, work on E: drive.
