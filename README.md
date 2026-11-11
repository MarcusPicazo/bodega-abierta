# Bodega Abierta — Core API

Open-source (Apache-2.0) warehouse management API for Mexican food banks, by RapiJazz A.C.

> Status: **Phase 3** — versioned policy sets with an approval workflow and an automatic audit trail
> (on top of Phase 2: donors, products, intake → inventory lots, validation, optimistic concurrency).

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
