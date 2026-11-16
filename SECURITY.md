# Seguridad / Security

Bodega Abierta guarda datos de donantes y de personas beneficiarias, así que tomamos en serio cualquier
falla de seguridad.

## Cómo reportar una vulnerabilidad

**No abras un issue público.** Usa el reporte privado de GitHub: pestaña **Security** →
**Report a vulnerability** en este repositorio.

Incluye, si puedes:

- Qué encontraste y qué permite hacer (por ejemplo: leer o modificar datos de otro banco de alimentos).
- Los pasos para reproducirlo.
- La versión o el commit donde lo viste.

Respondemos en cuanto podamos, te decimos si lo confirmamos y te avisamos cuando esté corregido. Si
quieres, te damos crédito en la nota del cambio.

## Qué consideramos crítico

- Que un banco de alimentos pueda leer o escribir datos de otro (aislamiento multi-tenant).
- Exposición de datos personales de beneficiarios o donantes, incluido el log de auditoría.
- Saltarse los permisos de rol (por ejemplo, aprobar una política sin ser `Coordinator`).

## Versiones con soporte

Mientras el proyecto esté antes de la versión 1.0, solo se corrige la rama `main`.

---

*English: please report vulnerabilities privately through GitHub's **Security → Report a
vulnerability** tab, not in a public issue.*
