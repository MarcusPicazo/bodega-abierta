# Bodega Abierta — Core API

Open-source (Apache-2.0) warehouse management API for Mexican food banks, by RapiJazz A.C.

> Status: **Phase 1** — skeleton, multi-tenant `DbContext`, first migration, health checks, CI.

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
