# Bodega Abierta — API central

[![CI](https://github.com/MarcusPicazo/bodega-abierta/actions/workflows/ci.yml/badge.svg)](https://github.com/MarcusPicazo/bodega-abierta/actions/workflows/ci.yml)
[![Licencia: Apache-2.0](https://img.shields.io/badge/licencia-Apache--2.0-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)

[English](README.md) · **Español**

API de código abierto (Apache-2.0) para administrar las bodegas de los bancos de alimentos en México.
Es un proyecto de RapiJazz (asociación civil en formación). La PWA offline que se usa en la bodega es
un cliente aparte que va a consumir esta API.

> Estado: **Fase 4**. Motor de reglas sobre la política activa, asignación FEFO para las entregas,
> horarios de recolección con cupo y `POST /api/v1/policies/evaluate`, encima de las fases 1–3
> (multi-tenant, recepción de inventario, políticas con versiones y aprobación, auditoría).

## Qué resuelve

Muchos bancos de alimentos llevan su inventario en Excel o en papel. Así es difícil saber qué está
por caducar, cuánto se entregó a cada familia o cuánta comida se rescató en el mes. Bodega Abierta
registra cada donativo, entrega primero lo que caduca primero (FEFO), aplica las reglas de cada banco
y deja un historial auditable de todos los cambios.

## API (v1)

Toda llamada a `/api/v1/...` necesita el encabezado `X-FoodBank-Id` (el banco de alimentos).

| Recurso | Endpoints |
|---|---|
| `donors` (donantes) | `POST`, `GET` (paginado), `GET {id}`, `PUT {id}` (requiere `If-Match`) |
| `products` (productos) | `POST`, `GET` (paginado, `?category=`), `GET {id}`, `PUT {id}` (requiere `If-Match`) |
| `storage-locations` (ubicaciones) | `POST`, `GET` |
| `donation-intakes` (recepciones) | `POST` (crea un lote por línea, todo o nada), `GET {id}` |
| `inventory-lots` (lotes) | `GET` (orden FEFO; `?productId=&status=&expiringOnOrBefore=`), `GET {id}` |
| `policies` 🔒 (políticas) | `POST` (borrador, opcional `copyFromPolicySetId`), `GET`, `GET active`, `GET {id}`, `PUT {id}`, `POST {id}/submit`, `POST {id}/approve` 👑, `POST {id}/reject` 👑 |
| `audit-entries` 👑 (auditoría) | `GET` (`?entityType=&entityId=`) |
| `policies/evaluate` 🔒 | `POST` simulación → `{ allowed, policySetId, policySetVersion, violations[], allocation[] }` |
| `beneficiaries` (beneficiarios) | `POST`, `GET` (paginado), `GET {id}` |
| `pickup-slots` 🔒 (horarios) | `POST` (se valida contra `PickupWindow`), `GET` (`?from=&to=`), `GET {id}` |
| `distributions` 🔒 (entregas) | `POST` (asignación FEFO y reserva de horario; 422 con `violations`, 409 si el stock o el horario cambió al mismo tiempo), `GET {id}` |
| `inventory-lots/expiry-alerts` | `GET` lotes que caducan dentro de los días de la regla `ExpiryAlert` activa |

🔒 requiere un usuario · 👑 requiere el rol `Coordinator`. Mientras no exista JWT/OIDC, los ambientes
Development y Testing aceptan los encabezados `X-User-Id` y `X-User-Roles` (en cualquier otro ambiente
se ignoran).

### Motor de reglas

Cada tipo de regla es una estrategia (`IPolicyRuleEvaluator`) con parámetros JSON tipados, que se
validan al crear o editar una política:

| Regla | Parámetros (ejemplo) | Se aplica a |
|---|---|---|
| `FefoAllocation` | `{}` | entregas: un lote elegido a mano no puede saltarse otro que caduca antes |
| `ExpiryAlert` | `{"daysBeforeExpiry":3}` | entregas (solo aviso) y `expiry-alerts` |
| `PickupWindow` | `{"days":["Tuesday","Thursday"],"open":"09:00","close":"14:00","maxSlotCapacity":40}` | horarios y entregas |
| `BeneficiaryLimit` | `{"maxKg":25,"periodDays":7}` | entregas (solo productos en kg) |
| `DonorDropOffHours` | `{"days":["Monday","Friday"],"open":"08:00","close":"12:00"}` | recepciones |

Las horas son locales a `timeZone` (por defecto `America/Mexico_City`). Las validaciones básicas
(stock, cupo del horario, beneficiario inactivo, horario en el pasado) se aplican aunque no haya
política activa.

Una política pasa por Borrador → Pendiente → Activa → Retirada; si se rechaza, regresa de Pendiente a
Borrador. Quien la envía no puede aprobar su propia versión, y un índice único filtrado garantiza que
haya a lo más una versión activa por banco. Cada alta, cambio o baja queda en `AuditEntries` gracias a
un `SaveChangesInterceptor` de EF Core (valores antes y después en JSON, quién y cuándo), dentro de la
misma transacción.

Los errores siguen RFC 9457 (ProblemDetails): 400 validación, 404 no encontrado, 409 duplicado,
412 `If-Match` desactualizado, 422 regla de negocio (con un `code`, por ejemplo `lot.expiry_required`),
428 falta `If-Match`.

## Correrlo en tu computadora

Necesitas el SDK de .NET 10, Docker y `dotnet-ef` (`dotnet tool install -g dotnet-ef`).

```bash
cp .env.example .env            # luego pon una contraseña fuerte en MSSQL_SA_PASSWORD
docker compose up -d            # SQL Server en localhost:1434 (contenedor: bodega-abierta-sql)

dotnet user-secrets set "ConnectionStrings:BodegaAbierta" \
  "Server=localhost,1434;Database=BodegaAbierta;User Id=sa;Password=<tu contraseña>;TrustServerCertificate=True" \
  --project src/BodegaAbierta.Api

dotnet ef database update -p src/BodegaAbierta.Infrastructure -s src/BodegaAbierta.Api
dotnet run --project src/BodegaAbierta.Api --launch-profile http
curl http://localhost:5194/health
```

## Tests

```bash
dotnet test   # los tests de integración levantan su propio SQL Server con Testcontainers
```

## Multi-tenant

Cada entidad que pertenece a un banco implementa `ITenantOwned`. `BodegaAbiertaDbContext` les agrega a
todas un filtro global (`Tenant`) sobre `FoodBankId`. El banco sale del encabezado `X-FoodBank-Id`; sin
banco, las consultas no regresan nada. Ver `docs/adr/0001-dotnet-backend.md`.

El filtro solo protege las lecturas. Todavía falta el guardián de escrituras en `SaveChangesAsync`
(ver el test marcado como `Skip` en `TenantIsolationTests`).

## Hoja de ruta

| Fase | Alcance | Estado |
|---|---|---|
| 1 | Esqueleto, DbContext multi-tenant, primera migración, CI | ✅ |
| 2 | Donantes, productos, recepción → lotes de inventario | ✅ |
| 3 | Políticas con versiones, aprobación y auditoría | ✅ |
| 4 | Motor de reglas, entregas FEFO, horarios de recolección | ✅ |
| — | Libro mayor de inventario para sincronizar offline ([ADR 0002](docs/adr/0002-inventory-ledger.md)) | Propuesto |
| 5 | Tests completos, guardián de escrituras por banco | En curso |
| 6 | Reporte de impacto, observabilidad, runbook | Planeado |

## Documentación

- [Guía de arquitectura](docs/arquitectura.md): las capas, el recorrido de una petición y dónde va el código nuevo.
- [Registros de decisiones (ADR)](docs/adr/).
- [Cómo contribuir](CONTRIBUTING.md) · [Seguridad](SECURITY.md) · [Código de conducta](CODE_OF_CONDUCT.md).

## Cómo usamos IA

El código se escribe con ayuda de IA (Claude Code), dirigida y revisada por quien mantiene el
proyecto. `CLAUDE.md` tiene las reglas que toda sesión de IA debe seguir. Algunas partes se dejan a
propósito como tareas `TODO(nombre):` para escribirlas a mano, de modo que cualquiera que mantenga el
proyecto pueda trabajar en él sin IA. Si contribuyes, también puedes usar IA, con las reglas de
[CONTRIBUTING.md](CONTRIBUTING.md).

## Licencia

[Apache License 2.0](LICENSE).
