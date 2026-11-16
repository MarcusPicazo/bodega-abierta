# Bodega Abierta — Core API

[![CI](https://github.com/MarcusPicazo/bodega-abierta/actions/workflows/ci.yml/badge.svg)](https://github.com/MarcusPicazo/bodega-abierta/actions/workflows/ci.yml)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)

**English** · [Español](README.es.md)

Open-source (Apache-2.0) warehouse management API for Mexican food banks, a project of RapiJazz
(a civil association in formation). The offline-first PWA used in the warehouse is a separate client
that will consume this API.

> Status: **Phase 4** — rules engine (strategy pattern) over the Active policy set, FEFO allocation for
> distributions, pickup slots with capacity, and `POST /api/v1/policies/evaluate`
> (on top of Phases 1–3: multi-tenancy, inventory intake, versioned policies with approval, audit trail).

## API (v1)

Every `/api/v1/...` call needs the `X-FoodBank-Id` header.

| Resource | Endpoints |
|---|---|
| `donors` | `POST`, `GET` (paged), `GET {id}`, `PUT {id}` (requires `If-Match`) |
| `products` | `POST`, `GET` (paged, `?category=`), `GET {id}`, `PUT {id}` (requires `If-Match`) |
| `storage-locations` | `POST`, `GET` |
| `donation-intakes` | `POST` (creates one inventory lot per line, all or nothing), `GET {id}` |
| `inventory-lots` | `GET` (FEFO order; `?productId=&status=&expiringOnOrBefore=`), `GET {id}` |
| `policies` 🔒 | `POST` (draft, optional `copyFromPolicySetId`), `GET`, `GET active`, `GET {id}`, `PUT {id}`, `POST {id}/submit`, `POST {id}/approve` 👑, `POST {id}/reject` 👑 |
| `audit-entries` 👑 | `GET` (`?entityType=&entityId=`) |
| `policies/evaluate` 🔒 | `POST` dry run → `{ allowed, policySetId, policySetVersion, violations[], allocation[] }` |
| `beneficiaries` | `POST`, `GET` (paged), `GET {id}` |
| `pickup-slots` 🔒 | `POST` (checked against `PickupWindow`), `GET` (`?from=&to=`), `GET {id}` |
| `distributions` 🔒 | `POST` (FEFO allocation + slot booking; 422 with `violations`, 409 on concurrent stock/slot changes), `GET {id}` |
| `inventory-lots/expiry-alerts` | `GET` lots expiring within the Active policy's `ExpiryAlert` days |

### Rules engine

Each rule type is a strategy (`IPolicyRuleEvaluator`) with typed JSON parameters, validated when a
policy set is created or edited:

| Rule | Parameters (example) | Applies to |
|---|---|---|
| `FefoAllocation` | `{}` | distributions: a hand-picked lot must not skip an earlier-expiring one |
| `ExpiryAlert` | `{"daysBeforeExpiry":3}` | distributions (warning only) and `expiry-alerts` |
| `PickupWindow` | `{"days":["Tuesday","Thursday"],"open":"09:00","close":"14:00","maxSlotCapacity":40}` | pickup slots and distributions |
| `BeneficiaryLimit` | `{"maxKg":25,"periodDays":7}` | distributions (kilogram products only) |
| `DonorDropOffHours` | `{"days":["Monday","Friday"],"open":"08:00","close":"12:00"}` | donation intakes |

Times are local to `timeZone` (default `America/Mexico_City`). Built-in checks (stock, slot capacity,
inactive beneficiary, slot in the past) apply even without an Active policy.

🔒 requires a user · 👑 requires the `Coordinator` role. Until real JWT/OIDC exists, Development and
Testing accept the `X-User-Id` and `X-User-Roles` headers (ignored in any other environment).

Policy sets move Draft → PendingApproval → Active → Retired (rejection sends PendingApproval back to
Draft). The submitter cannot approve their own version, and a filtered unique index guarantees at most
one Active version per food bank. Every create/update/delete is recorded in `AuditEntries` by an EF
Core `SaveChangesInterceptor` (changed values before/after as JSON, actor, timestamp) in the same transaction.

Errors are RFC 9457 ProblemDetails: 400 validation, 404 not found, 409 duplicate, 412 stale
`If-Match`, 422 business rule (with a `code`, e.g. `lot.expiry_required`), 428 missing `If-Match`.

## Run locally

Requirements: .NET 10 SDK, Docker, `dotnet-ef` (`dotnet tool install -g dotnet-ef`).

```bash
cp .env.example .env            # then set a strong MSSQL_SA_PASSWORD
docker compose up -d            # SQL Server on localhost:1434 (container: bodega-abierta-sql)

dotnet user-secrets set "ConnectionStrings:BodegaAbierta" \
  "Server=localhost,1434;Database=BodegaAbierta;User Id=sa;Password=<your password>;TrustServerCertificate=True" \
  --project src/BodegaAbierta.Api

dotnet ef database update -p src/BodegaAbierta.Infrastructure -s src/BodegaAbierta.Api
dotnet run --project src/BodegaAbierta.Api --launch-profile http
curl http://localhost:5194/health
```

## Test

```bash
dotnet test   # integration tests start their own SQL Server container via Testcontainers
```

## Multi-tenancy

Every tenant-owned entity implements `ITenantOwned`. `BodegaAbiertaDbContext` adds a named global
query filter (`Tenant`) on `FoodBankId` to all of them. The tenant comes from the `X-FoodBank-Id`
request header; with no tenant, queries return nothing. See `docs/adr/0001-dotnet-backend.md`.

Query filters only protect reads. A write guard in `SaveChangesAsync` is still pending (see the
skipped test in `TenantIsolationTests`).

## Roadmap

| Phase | Scope | Status |
|---|---|---|
| 1 | Skeleton, multi-tenant DbContext, first migration, CI | ✅ |
| 2 | Donors, products, donation intake → inventory lots | ✅ |
| 3 | Versioned policy sets, approval workflow, audit trail | ✅ |
| 4 | Rules engine, FEFO distributions, pickup slots | ✅ |
| — | Append-only inventory ledger for offline sync ([ADR 0002](docs/adr/0002-inventory-ledger.md)) | Proposed |
| 5 | Full test coverage, tenant write guard | In progress |
| 6 | Impact report, observability, runbook | Planned |

## Documentation

- [Architecture guide (Spanish)](docs/arquitectura.md): layers, the path of one request, where to put new code.
- [Architecture decision records](docs/adr/).
- [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md) · [Code of conduct](CODE_OF_CONDUCT.md).

## How this project uses AI

The code is written with AI assistance (Claude Code), directed and reviewed by the maintainer.
`CLAUDE.md` holds the project rules every AI session must follow. Some parts are deliberately left as
`TODO(name):` tasks to be written by hand, so that every maintainer can work on the code without AI.
Contributors may use AI too, under the rules in [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[Apache License 2.0](LICENSE).
