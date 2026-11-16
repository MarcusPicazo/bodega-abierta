# Cómo contribuir a Bodega Abierta

*English speakers: issues and pull requests in English are welcome too.*

Gracias por ayudar. Este proyecto lo usan bancos de alimentos reales, así que un error puede significar
comida que caduca o una familia que no recibe su despensa. Por eso las reglas de abajo no son opcionales.

## Antes de empezar

1. Lee la [guía de arquitectura](docs/arquitectura.md). Son 15 minutos y te ahorran días.
2. Corre el proyecto y los tests en tu computadora siguiendo el [README](README.es.md).
3. Busca un issue con la etiqueta `good first issue` o pregunta en uno antes de empezar algo grande.

## El flujo

1. Crea una rama desde `main` con un nombre claro: `feat/ajustes-de-merma`, `fix/fefo-sin-caducidad`.
2. Haz cambios pequeños. Un PR que toca 40 archivos casi nadie lo puede revisar bien.
3. Cada cambio de comportamiento lleva su test: unitario para el dominio y las reglas, de integración
   para los endpoints.
4. `dotnet build` y `dotnet test` deben pasar en tu computadora antes de abrir el PR.
5. Abre el PR y llena la plantilla completa.
6. Nada entra a `main` sin una revisión aprobada y el CI en verde.

## Reglas del código

- La lógica va en `Domain` y `Application`, nunca en los controllers.
- Todo es `async` y recibe `CancellationToken`.
- Cada entidad que pertenece a un banco implementa `ITenantOwned`. No uses `IgnoreQueryFilters` sin
  explicar en el PR por qué.
- Los errores de negocio se lanzan como `DomainRuleException` con un `code` estable
  (por ejemplo `lot.expiry_required`), porque la PWA depende de esos códigos.
- Un cambio de base de datos va con su migración de EF Core, en el mismo PR.
- Nunca subas contraseñas, cadenas de conexión ni datos reales de beneficiarios o donantes. Usa
  `user-secrets` y `.env` (que ya están ignorados por git).

## Uso de IA

Puedes usar Claude Code, Copilot o la herramienta que prefieras. A cambio:

- **Tú respondes por cada línea.** Si no puedes explicar un cambio, no lo subas.
- **Dilo en el PR.** La plantilla pregunta qué partes se hicieron con IA. No es para juzgarte, es para
  revisarlo mejor.
- **En la revisión te pueden pedir que expliques una parte en vivo.** Si no puedes, el PR espera hasta
  que puedas.
- **Las tareas `TODO(nombre):` se hacen a mano.** Están pensadas para que aprendas el proyecto; si se
  las das a la IA, pierden su sentido.
- `CLAUDE.md` tiene las reglas del proyecto para las herramientas de IA. Si una regla cambia, se cambia
  ahí en el mismo PR.

## Becarios de servicio social

Tu primer mes es para aprender el proyecto:

1. Semana 1: corre todo, lee la guía de arquitectura y sigue una petición de principio a fin con el
   depurador.
2. Semanas 2–4: resuelve 2 o 3 tareas `TODO(Becario)` a mano, con tests que ya están escritos.
3. Después: tu propio issue, revisado por quien mantiene el proyecto o por un becario del semestre
   anterior.

## Reportar errores

Usa la plantilla de issue. Si es una falla de seguridad (por ejemplo, un banco puede ver datos de otro),
**no** abras un issue público: sigue [SECURITY.md](SECURITY.md).
