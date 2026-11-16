# Guía de arquitectura

Esta guía es para quien llega nuevo al proyecto, sobre todo becarios. Explica cómo está organizado el
código, qué pasa en una petición de principio a fin y dónde va el código nuevo. Léela con el código
abierto al lado.

## 1. Las cuatro capas

```
            ┌────────────────────────────┐
  HTTP  ──▶ │  Api                       │  controllers, autenticación, encabezados, errores HTTP
            └─────────────┬──────────────┘
                          ▼
            ┌────────────────────────────┐
            │  Application               │  casos de uso (servicios), validación, motor de reglas
            └─────────────┬──────────────┘
                          ▼
            ┌────────────────────────────┐
            │  Domain                    │  entidades y reglas de negocio puras (sin EF, sin HTTP)
            └────────────────────────────┘
                          ▲
            ┌─────────────┴──────────────┐
            │  Infrastructure            │  EF Core, SQL Server, migraciones, auditoría
            └────────────────────────────┘
```

La regla de oro: **las flechas solo apuntan hacia `Domain`**. `Domain` no conoce a nadie. `Application`
conoce a `Domain` y define interfaces (`IBodegaDbContext`, `ITenantContext`, `ICurrentUser`) que
`Infrastructure` y `Api` implementan. Así las reglas de negocio se pueden probar sin base de datos.

| Capa | Proyecto | Qué va ahí | Qué NO va ahí |
|---|---|---|---|
| Api | `src/BodegaAbierta.Api` | Controllers delgados, `Program.cs`, autenticación, ETags, mapeo de errores a HTTP | Lógica de negocio, consultas a la base |
| Application | `src/BodegaAbierta.Application` | Servicios por caso de uso, contratos (request/response), validadores, motor de reglas | Detalles de SQL o de HTTP |
| Domain | `src/BodegaAbierta.Domain` | Entidades con sus reglas (`InventoryLot.Allocate`, `PolicySet.Approve`), excepciones de negocio | Referencias a EF Core, ASP.NET o paquetes externos |
| Infrastructure | `src/BodegaAbierta.Infrastructure` | `BodegaAbiertaDbContext`, configuraciones de tablas, migraciones, interceptor de auditoría | Reglas de negocio |

## 2. El recorrido de una petición

Ejemplo: registrar un donativo con `POST /api/v1/donation-intakes`.

1. **`Program.cs`** ya registró los servicios, el versionado de la API y el manejador de errores.
2. **`HttpTenantContext`** lee el encabezado `X-FoodBank-Id` y dice de qué banco es la petición.
3. **`DonationIntakesController`** recibe el JSON y llama a `DonationIntakeService.CreateAsync`. No hace
   nada más: los controllers no tienen lógica.
4. **`DonationIntakeService`** (Application):
   - Exige que haya banco (`tenant.RequireFoodBankId()`).
   - Valida el request con FluentValidation (`CreateDonationIntakeRequestValidator`).
   - Busca el donante, los productos y las ubicaciones. El **filtro global por banco** hace que un id
     de otro banco simplemente "no exista".
   - Revisa las reglas de la política activa con `PolicyGate` (por ejemplo `DonorDropOffHours`).
   - Crea la entidad `DonationIntake` y sus `InventoryLot` (Domain), que validan sus propias reglas.
5. **`SaveChangesAsync`** guarda todo en una sola transacción. Antes de mandar el SQL,
   **`AuditSaveChangesInterceptor`** agrega una fila de auditoría por cada cambio, en la misma transacción.
6. Si algo falla, una excepción sube hasta **`ProblemDetailsExceptionHandler`**, que la convierte en la
   respuesta HTTP correcta:

| Excepción | HTTP |
|---|---|
| `ValidationException` (FluentValidation) | 400 |
| `TenantNotResolvedException` | 400 |
| `UnauthenticatedException` / `ForbiddenException` | 401 / 403 |
| `NotFoundException` | 404 |
| `ConcurrencyConflictException`, valor único duplicado | 409 |
| `DbUpdateConcurrencyException`, `If-Match` viejo | 412 |
| `DomainRuleException`, `PolicyViolationException` | 422 (con `code` o `violations`) |
| Falta `If-Match` | 428 |

Para verlo con tus propios ojos: pon un breakpoint en el controller y avanza paso a paso con el depurador.

## 3. Conceptos que debes entender

**Multi-tenant.** Cada banco de alimentos es un *tenant*. Toda entidad que le pertenece implementa
`ITenantOwned` y `BodegaAbiertaDbContext` le aplica el filtro `FoodBankId == banco actual` a todas las
consultas, automáticamente. El filtro lee un miembro del contexto (`CurrentFoodBankId`) y no una
constante, para que EF Core lo mande como parámetro SQL y un solo modelo sirva para todos los bancos.
Ojo: el filtro protege las **lecturas**, no las escrituras.

**Concurrencia optimista.** Las entidades que implementan `IVersioned` tienen un `RowVersion`. El `GET`
lo devuelve como `ETag`; el `PUT` lo manda de regreso en `If-Match`. Si alguien más cambió el registro
mientras tanto, la versión ya no coincide y la API responde 412 en lugar de pisar el cambio ajeno.

**Auditoría.** No se escribe a mano: el interceptor registra cada alta, cambio y baja con los valores
antes y después, quién y cuándo. Si agregas una entidad, se audita sola.

**Motor de reglas (patrón strategy).** Cada tipo de regla (`FefoAllocation`, `PickupWindow`, etc.) es
una clase que implementa `IPolicyRuleEvaluator`. `PolicyEngine` elige la clase según el tipo y la
ejecuta, sin saber qué hace cada regla. Para agregar un tipo nuevo de regla agregas una clase y la
registras; no tocas el motor.

**FEFO.** *First Expired, First Out*: se entrega primero lo que caduca primero. Lo calcula `FefoPicker`
en Domain.

**Políticas con versiones.** Las reglas de un banco viven en un `PolicySet` que pasa por
Borrador → Pendiente → Activa → Retirada. Solo una versión puede estar activa a la vez, y quien la envía
no puede aprobarla.

## 4. Dónde va el código nuevo

| Quiero… | Lo pongo en… |
|---|---|
| Una regla de negocio de una entidad ("un lote caducado no se puede asignar") | Un método de la entidad en `Domain` |
| Un caso de uso nuevo ("registrar merma") | Un método de servicio en `Application`, con su request, response y validador |
| Un endpoint nuevo | Un método en el controller de `Api/Controllers/V1` que solo llama al servicio |
| Una tabla o columna nueva | La entidad en `Domain`, su configuración en `Infrastructure/Persistence/Configurations` y una migración |
| Un tipo de regla nuevo | Un evaluador en `Application/Rules/Evaluators` y un valor nuevo en `RuleType` |

Crear una migración:

```bash
dotnet ef migrations add NombreDelCambio -p src/BodegaAbierta.Infrastructure -s src/BodegaAbierta.Api
```

## 5. Tests

- **Unitarios** (`tests/BodegaAbierta.UnitTests`): entidades, reglas, el motor. Rápidos, sin base de datos.
- **De integración** (`tests/BodegaAbierta.IntegrationTests`): endpoints completos contra un SQL Server
  real que Testcontainers levanta en Docker. Aquí se prueba que un banco no puede leer datos de otro.

Un test marcado con `Skip` y un mensaje `TODO(...)` espera código que alguien debe escribir a mano.

## 6. Lo que viene

- El inventario va a pasar de una cantidad editable (`QuantityOnHand`) a un **libro mayor de
  movimientos**, para que la PWA pueda trabajar sin internet. Ver
  [ADR 0002](adr/0002-inventory-ledger.md).
- Autenticación real con JWT/OIDC, en lugar de los encabezados de desarrollo.
