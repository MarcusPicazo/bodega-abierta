# ADR 0001 — C# / ASP.NET Core backend

- **Status:** Accepted
- **Date:** 2026-11-11

## Context

Bodega Abierta is an open-source (Apache-2.0) warehouse management system for Mexican food banks,
run by RapiJazz A.C. It needs a multi-tenant API (one tenant per food bank), a rules engine for
allocation and pickup scheduling, auditable changes, and efficient reporting queries. An
offline-first PWA (Next.js) will be built later and will consume this API.

## Decision

The backend is a **C# / ASP.NET Core** Web API on the current .NET LTS, with:

- Clean-architecture layout: `Domain` → `Application` → `Infrastructure` / `Api`.
- **EF Core + SQL Server**, with versioned migrations.
- **Multi-tenancy through an EF Core global query filter** on `FoodBankId`, applied automatically
  to every entity that implements `ITenantOwned`. The tenant is resolved per request
  (`X-FoodBank-Id` header for now, an authentication claim later).
- `Asp.Versioning` (URL segment `/api/v1/...`), OpenAPI document per version, ProblemDetails for errors.
- xUnit v3 + NSubstitute + FluentAssertions; Testcontainers for integration tests against real SQL Server.
- GitHub Actions CI.

The PWA is a separate client and does not share code with this repository.

## Consequences

- Strong typing, mature tooling, and EF Core features (query filters, interceptors, concurrency
  tokens) that cover multi-tenancy and auditing directly.
- The pattern matches the maintainer's other .NET projects, so knowledge carries over.
- SQL Server needs Docker for local development (`docker-compose.yml`, host port **1434**).
- A shared database with a tenant column is cheaper to operate than one database per food bank,
  but isolation depends on the query filter. Integration tests must prove that tenant A cannot
  read tenant B, and writes need their own guard (query filters only cover reads).
